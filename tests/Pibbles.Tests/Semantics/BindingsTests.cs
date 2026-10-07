using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public class BindingsTests
{
    private const string Story = """
        @prefix kitchen
        @enum position: left, right
        @actor mira
            name: Mira
        @command show(who: actor, at: position, delay: duration = 0s)
        @function has_item(item: string) -> bool
        @var $count = 0
        @var $wait = 1s

        == .door
        @show mira left 1
        @wait 1
        @set $count += 1
        @set $wait += 1
        @if has_item("key") and $count > 0
            @jump .hall

        == .hall
        mira: Hi.
        """;

    [Fact]
    public void Bindings_Variable_ResolvesToItsSymbol()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        var set = Assert.IsType<SetStatementSyntax>(Statements(tree).First(statement => statement is SetStatementSyntax));

        Assert.Equal("count", Assert.IsType<VariableSymbol>(compilation.Bindings.SymbolOf(set.Variable)).Name);
    }

    [Fact]
    public void Bindings_Call_ResolvesFunctionName()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        CallExpressionSyntax call = Statements(tree).SelectMany(SyntaxWalk.ExpressionsOf).OfType<CallExpressionSyntax>().Single();

        Assert.Equal("has_item", Assert.IsType<FunctionSymbol>(compilation.Bindings.SymbolOf(call.Function)).Name);
        Assert.Equal(TypeSymbol.Bool, compilation.Bindings.TypeOf(call)?.Type);
    }

    [Fact]
    public void Bindings_BareNamesInArguments_ResolveByExpectedType()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        var show = Assert.IsType<CommandStatementSyntax>(Statements(tree).First(statement => statement is CommandStatementSyntax));
        var actor = (NameExpressionSyntax)show.Arguments[0].Value;
        var member = (NameExpressionSyntax)show.Arguments[1].Value;

        Assert.IsType<ActorSymbol>(compilation.Bindings.SymbolOf(actor));
        Assert.Equal("left", Assert.IsType<EnumMemberSymbol>(compilation.Bindings.SymbolOf(member)).Name);
        Assert.Equal(compilation.Symbols.Enums["position"], compilation.Bindings.TypeOf(member)?.Type);
        Assert.IsType<CommandSymbol>(compilation.Bindings.SymbolOf(show.Command));
    }

    [Fact]
    public void Bindings_RelativeNodeName_ResolvesToTheFullNode()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        var jump = Assert.IsType<JumpStatementSyntax>(Statements(tree).First(statement => statement is JumpStatementSyntax));

        Assert.Equal("kitchen.hall", Assert.IsType<NodeSymbol>(compilation.Bindings.SymbolOf(jump.Target)).Name);
    }

    [Fact]
    public void Bindings_SpeakerAndNamedArgument_Resolve()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story + "\n@show mira left delay=2\n");

        var line = Assert.IsType<TextLineSyntax>(Statements(tree).First(statement => statement is TextLineSyntax));

        Assert.IsType<ActorSymbol>(compilation.Bindings.SymbolOf(line.Speaker!));
    }

    [Fact]
    public void Bindings_NumberWhereDurationIsExpected_IsConverted()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        ExpressionSyntax delay = ((CommandStatementSyntax)Statements(tree).First(statement => statement is CommandStatementSyntax)).Arguments[2].Value;
        ExpressionSyntax wait = ((WaitStatementSyntax)Statements(tree).First(statement => statement is WaitStatementSyntax)).Duration;
        ExpressionSyntax added = ((SetStatementSyntax)Statements(tree).Last(statement => statement is SetStatementSyntax)).Value;

        Assert.All([delay, wait, added], expression =>
            Assert.Equal(new ExpressionType(TypeSymbol.Number, TypeSymbol.Duration), compilation.Bindings.TypeOf(expression)));
    }

    [Fact]
    public void Bindings_NumberWhereNumberIsExpected_IsNotConverted()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        ExpressionSyntax added = ((SetStatementSyntax)Statements(tree).First(statement => statement is SetStatementSyntax)).Value;

        Assert.Equal(new ExpressionType(TypeSymbol.Number, TypeSymbol.Number), compilation.Bindings.TypeOf(added));
    }

    [Fact]
    public void Bindings_NumberBesideDuration_IsConvertedButFactorIsNot()
    {
        (Compilation compilation, SyntaxTree tree) = Compile("@var $d = 1s\n\n== a.b\n@set $d = $d + 1\n@set $d = $d * 2\n");

        SetStatementSyntax[] sets = [.. Statements(tree).OfType<SetStatementSyntax>()];
        var sum = (BinaryExpressionSyntax)sets[0].Value;
        var product = (BinaryExpressionSyntax)sets[1].Value;

        Assert.Equal(TypeSymbol.Duration, compilation.Bindings.TypeOf(sum.Right)?.ConvertedType);
        Assert.Equal(TypeSymbol.Number, compilation.Bindings.TypeOf(product.Right)?.ConvertedType);
    }

    [Theory]
    [InlineData("$d > 2")]
    [InlineData("$d == 1")]
    [InlineData("2 < $d")]
    public void Bindings_NumberComparedWithDuration_IsConverted(string condition)
    {
        (Compilation compilation, SyntaxTree tree) = Compile($"@var $d = 1s\n\n== a.b\n@if {condition}\n    @end\n");

        var comparison = (BinaryExpressionSyntax)((IfStatementSyntax)Statements(tree).First(statement => statement is IfStatementSyntax)).Condition;
        ExpressionSyntax number = comparison.Left is NumberLiteralSyntax ? comparison.Left : comparison.Right;
        ExpressionSyntax duration = ReferenceEquals(number, comparison.Left) ? comparison.Right : comparison.Left;

        Assert.Equal(new ExpressionType(TypeSymbol.Number, TypeSymbol.Duration), compilation.Bindings.TypeOf(number));
        Assert.Equal(new ExpressionType(TypeSymbol.Duration, TypeSymbol.Duration), compilation.Bindings.TypeOf(duration));
    }

    [Fact]
    public void Bindings_PositionalNamedAndMixedArguments_FillTheirParameters()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story + "\n@show mira left 1s\n@show mira at=right\n@show who=mira at=left\n@show mira left delay=2\n@show delay=2 at=left who=mira\n");

        CommandStatementSyntax[] shows = [.. Statements(tree).OfType<CommandStatementSyntax>()];

        Assert.Equal(["who", "at", "delay"], ParameterNames(compilation, shows[1]));
        Assert.Equal(["who", "at"], ParameterNames(compilation, shows[2]));
        Assert.Equal(["who", "at"], ParameterNames(compilation, shows[3]));
        Assert.Equal(["who", "at", "delay"], ParameterNames(compilation, shows[4]));
        Assert.Equal(["delay", "at", "who"], ParameterNames(compilation, shows[5]));
    }

    [Fact]
    public void Bindings_FunctionCallArguments_FillTheirParameters()
    {
        (Compilation compilation, SyntaxTree tree) = Compile(Story);

        CallExpressionSyntax call = Statements(tree).SelectMany(SyntaxWalk.ExpressionsOf).OfType<CallExpressionSyntax>().Single();

        Assert.Equal("item", compilation.Bindings.ParameterOf(call.Arguments[0])?.Name);
    }

    [Fact]
    public void Bindings_ParameterAndVariable_KeepTheirDefaultAndStartingValue()
    {
        (Compilation compilation, _) = Compile(Story + "\n@var $count = 5\n");

        ParameterSymbol delay = compilation.Symbols.Commands["show"].Parameters[2];

        Assert.IsType<DurationLiteralSyntax>(delay.Default);
        Assert.Null(compilation.Symbols.Commands["show"].Parameters[0].Default);
        Assert.Equal(0m, Assert.IsType<NumberLiteralSyntax>(compilation.Symbols.Variables["count"].StartingValue).Value);
    }

    [Fact]
    public void Bindings_DuplicateUntypedVariable_IsReportedOnlyAsDuplicate()
    {
        (Compilation compilation, _) = Compile("@var $a = 1\n@var $a = \"x\"\n");

        string[] codes = [.. compilation.Diagnostics.Select(diagnostic => diagnostic.Code)];
        Assert.Contains("PIB2060", codes);
        Assert.DoesNotContain("PIB2031", codes);
    }

    private static string[] ParameterNames(Compilation compilation, CommandStatementSyntax command) =>
        [.. command.Arguments.Select(argument => compilation.Bindings.ParameterOf(argument.Value)?.Name ?? "?")];

    [Fact]
    public void Bindings_IdenticalNodesInDifferentFiles_AreBoundSeparately()
    {
        SourceText[] sources =
        [
            new("one.pib", "@prefix one\n\n== .a\n== .b\n@jump .a\n"),
            new("two.pib", "@prefix two\n\n== .a\n== .b\n@jump .a\n"),
        ];
        Compilation compilation = Compilation.Create(sources);

        JumpStatementSyntax[] jumps = [.. compilation.SyntaxTrees.Select(tree => (JumpStatementSyntax)Statements(tree).Single(statement => statement is JumpStatementSyntax))];

        Assert.Equal(jumps[0].Target, jumps[1].Target);
        Assert.Equal(["one.a", "two.a"], jumps.Select(jump => compilation.Bindings.SymbolOf(jump.Target)?.Name));
    }

    public static TheoryData<string> CompleteSources { get; } = [.. SnapshotInputs().Select(path => Path.GetRelativePath(RepositoryRoot.Path, path))];

    /// <summary>
    /// Every expression the binder reaches has a type, with <see cref="TypeSymbol.Error"/> for one it couldn't work out
    /// (an error node, or a name that doesn't resolve). Every name that resolved has a symbol.
    /// </summary>
    [Theory]
    [MemberData(nameof(CompleteSources))]
    public void Bindings_EveryExpression_HasAType(string path)
    {
        Compilation compilation = Compilation.Create([new(path, File.ReadAllText(Path.Combine(RepositoryRoot.Path, path)))]);

        Assert.All(compilation.SyntaxTrees.SelectMany(Expressions), expression => Assert.True(compilation.Bindings.TypeOf(expression) is not null, expression.ToString()));
    }

    [Fact]
    public void Bindings_KitchenStory_BindsEveryNameWithoutDiagnostics()
    {
        Compilation compilation = Compilation.Create(KitchenSources());
        ExpressionSyntax[] expressions = [.. compilation.SyntaxTrees.SelectMany(Expressions)];

        Assert.Empty(compilation.Diagnostics);
        Assert.All(expressions, expression => Assert.NotEqual(TypeSymbol.Error, compilation.Bindings.TypeOf(expression)?.Type));
        Assert.All(expressions.OfType<VariableExpressionSyntax>(), variable => Assert.NotNull(compilation.Bindings.SymbolOf(variable)));
        Assert.All(expressions.OfType<NameExpressionSyntax>(), name => Assert.NotNull(compilation.Bindings.SymbolOf(name)));
        Assert.All(expressions.OfType<CallExpressionSyntax>(), call => Assert.NotNull(compilation.Bindings.SymbolOf(call.Function)));
    }

    private static (Compilation Compilation, SyntaxTree Tree) Compile(string text)
    {
        Compilation compilation = Compilation.Create([new SourceText("story.pib", text)]);
        return (compilation, compilation.SyntaxTrees[0]);
    }

    private static IEnumerable<StatementSyntax> Statements(SyntaxTree tree) => SyntaxWalk.Statements(tree.Root.Nodes.SelectMany(node => node.Body));

    /// <summary>Every expression in a file, in declarations too, with every expression inside each one.</summary>
    private static IEnumerable<ExpressionSyntax> Expressions(SyntaxTree tree)
    {
        IEnumerable<ExpressionSyntax> declared = tree.Root.Declarations.SelectMany(declaration => declaration switch
        {
            VariableDeclarationSyntax variable => [variable.Value],
            CommandDeclarationSyntax command => Defaults(command.Parameters),
            MarkupDeclarationSyntax markup => Defaults(markup.Parameters),
            FunctionDeclarationSyntax function => Defaults(function.Parameters),
            _ => [],
        });

        return declared.SelectMany(SyntaxWalk.Subexpressions).Concat(Statements(tree).SelectMany(SyntaxWalk.ExpressionsOf));

        static IEnumerable<ExpressionSyntax> Defaults(IEnumerable<ParameterSyntax> parameters) => parameters.Select(parameter => parameter.Default).OfType<ExpressionSyntax>();
    }

    private static SourceText[] KitchenSources() =>
    [
        .. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "samples", "kitchen"), "*.pib", SearchOption.AllDirectories)
            .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path), File.ReadAllText(path))),
    ];

    private static IEnumerable<string> SnapshotInputs() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Snapshots"), "*.pib", SearchOption.AllDirectories);
}
