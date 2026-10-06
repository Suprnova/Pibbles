using System.Text;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Gathers every declaration and node name in a story, and the prelude's, into a <see cref="SymbolTable"/>. It expands
/// relative node names, and reports names that clash, are reserved or aren't ASCII, and types that don't exist.
/// </summary>
/// <remarks>
/// Enums are declared first and variables last, so every type is known before anything uses one, and a variable's
/// missing type can be suggested from every other name. A name with a problem is still declared, so the places that
/// use it don't report it again; a duplicate isn't, and the first declaration wins.
/// </remarks>
internal sealed class DeclarationPass(List<Diagnostic> diagnostics)
{
    private readonly SymbolTable symbols = new();
    private readonly Dictionary<string, SourceLocation> nodeNames = [];
    private SyntaxTree tree = null!;
    private bool builtIn;

    public static SymbolTable Run(IReadOnlyList<SyntaxTree> trees, List<Diagnostic> diagnostics)
    {
        var pass = new DeclarationPass(diagnostics);
        pass.ForEachFile(trees, pass.DeclareEnums);
        pass.ForEachFile(trees, pass.DeclareOthers);
        pass.ForEachFile(trees, pass.DeclareVariables);
        return pass.symbols;
    }

    private void ForEachFile(IReadOnlyList<SyntaxTree> trees, Action<FileSyntax> declare)
    {
        foreach (SyntaxTree file in (IEnumerable<SyntaxTree>)[Prelude.Tree, .. trees])
        {
            tree = file;
            builtIn = file == Prelude.Tree;
            declare(file.Root);
        }
    }

    private void DeclareEnums(FileSyntax file)
    {
        foreach (EnumDeclarationSyntax declaration in file.Declarations.OfType<EnumDeclarationSyntax>())
        {
            List<EnumMemberSymbol> members = DeclareList(declaration.Members, member => member, SymbolKind.EnumMember,
                $"the members of `{declaration.Name.Text}`", member => new EnumMemberSymbol(member.Text, Location(member.Span)));
            Declare(symbols.Enums, new EnumSymbol(declaration.Name.Text, Location(declaration.Name.Span), members), declaration.Name, SymbolKind.Enum);
        }
    }

    private void DeclareOthers(FileSyntax file)
    {
        foreach (DeclarationSyntax declaration in file.Declarations)
        {
            switch (declaration)
            {
                case ActorDeclarationSyntax actor:
                    DeclareActor(actor);
                    break;

                case CommandDeclarationSyntax command:
                    List<ParameterSymbol> commandParameters = DeclareParameters(command.Parameters, $"the parameters of `@{command.Name.Text}`");
                    Declare(symbols.Commands, new CommandSymbol(command.Name.Text, Location(command.Name.Span), commandParameters, command.IsInline, command.Waits), command.Name, SymbolKind.Command);
                    break;

                case MarkupDeclarationSyntax markup:
                    List<ParameterSymbol> markupParameters = DeclareParameters(markup.Parameters, $"the parameters of `[{markup.Name.Text}]`");
                    Declare(symbols.Markup, new MarkupSymbol(markup.Name.Text, Location(markup.Name.Span), markupParameters), markup.Name, SymbolKind.Markup);
                    break;

                case IconDeclarationSyntax icons:
                    foreach (NameSyntax icon in icons.Names)
                        Declare(symbols.Icons, new IconSymbol(icon.Text, Location(icon.Span)), icon, SymbolKind.Icon);
                    break;

                case TagDeclarationSyntax tags:
                    foreach (TagEntrySyntax tag in tags.Entries)
                        DeclareTag(tag);
                    break;

                case FunctionDeclarationSyntax function:
                    List<ParameterSymbol> functionParameters = DeclareParameters(function.Parameters, $"the parameters of `{function.Name.Text}()`");
                    var symbol = new FunctionSymbol(function.Name.Text, Location(function.Name.Span), functionParameters, ResolveType(function.ReturnType));
                    Declare(symbols.Functions, symbol, function.Name, SymbolKind.Function);
                    break;
            }
        }

        foreach (NodeSyntax node in file.Nodes)
            DeclareNode(node);
    }

    private void DeclareVariables(FileSyntax file)
    {
        foreach (VariableDeclarationSyntax declaration in file.Declarations.OfType<VariableDeclarationSyntax>())
        {
            var name = new NameSyntax(declaration.Variable.Name) { Span = declaration.Variable.Span };
            TypeSymbol type = declaration.Type is { } written ? ResolveType(written) : TypeOfValue(declaration);
            Declare(symbols.Variables, new VariableSymbol(name.Text, Location(name.Span), type), name, SymbolKind.Variable, $"${name.Text}");
        }
    }

    private void DeclareActor(ActorDeclarationSyntax actor)
    {
        string displayName = actor.DisplayName ?? actor.Name.Text;
        string poseList = actor.Name.IsMissing ? "this actor's poses" : $"{displayName}'s poses";
        List<PoseSymbol> poses = DeclareList(actor.Poses, pose => pose, SymbolKind.Pose, poseList, pose => new PoseSymbol(pose.Text, Location(pose.Span)));
        Declare(symbols.Actors, new ActorSymbol(actor.Name.Text, Location(actor.Name.Span), displayName, poses), actor.Name, SymbolKind.Actor);
    }

    private void DeclareTag(TagEntrySyntax tag)
    {
        TypeSymbol? type = tag.Type is { } written ? ResolveType(written) : null;
        if (type is not (null or EnumSymbol) && type != TypeSymbol.String && type != TypeSymbol.Error)
            Report(DiagnosticCatalog.TagValueType, tag.Type!.Span, type.Name);

        Declare(symbols.Tags, new TagSymbol(tag.Name.Text, Location(tag.Name.Span), type, tag.AllowsEmpty), tag.Name, SymbolKind.Tag);
    }

    private List<ParameterSymbol> DeclareParameters(IReadOnlyList<ParameterSyntax> parameters, string listName)
    {
        foreach (ParameterSyntax parameter in parameters.SkipWhile(parameter => parameter.Default is null).Where(parameter => parameter.Default is null && !parameter.Name.IsMissing))
            Report(DiagnosticCatalog.RequiredAfterOptional, parameter.Span, parameter.Name.Text);

        return DeclareList(parameters, parameter => parameter.Name, SymbolKind.Parameter, listName,
            parameter => new ParameterSymbol(parameter.Name.Text, Location(parameter.Name.Span), ResolveType(parameter.Type), parameter.Default is not null));
    }

    /// <summary>Declares the names in one declaration's list, such as an actor's poses, which have to differ from each other.</summary>
    private List<T> DeclareList<TSyntax, T>(IEnumerable<TSyntax> items, Func<TSyntax, NameSyntax> nameOf, SymbolKind kind, string listName, Func<TSyntax, T> create)
        where T : Symbol
    {
        List<T> declared = [];
        foreach (TSyntax item in items)
        {
            NameSyntax name = nameOf(item);
            if (name.IsMissing)
                continue;

            bool valid = CheckName(name, kind);
            if (declared.All(symbol => symbol.Name != name.Text))
                declared.Add(create(item));
            else if (valid)
                Report(DiagnosticCatalog.RepeatedName, name.Span, name.Text, listName);
        }

        return declared;
    }

    private void DeclareNode(NodeSyntax node)
    {
        if (FullName(node.Name) is not { } name || !DeclareNodeName(name, node.Name))
            return;

        List<string> aliases = [];
        var symbol = new NodeSymbol(name, Location(node.Name.Span), aliases);
        symbols.Nodes.Add(name, symbol);

        foreach (NameSyntax alias in node.Aliases)
        {
            if (FullName(alias) is { } old && DeclareNodeName(old, alias))
            {
                aliases.Add(old);
                symbols.Aliases.Add(old, symbol);
            }
        }
    }

    /// <summary>Claims a node name or alias, which share one namespace, reporting it if it's taken.</summary>
    private bool DeclareNodeName(string fullName, NameSyntax written)
    {
        bool valid = CheckName(written, SymbolKind.Node);
        if (nodeNames.TryGetValue(fullName, out SourceLocation first))
        {
            if (valid)
                Report(DiagnosticCatalog.DuplicateNode, written.Span, fullName, Where(first));

            return false;
        }

        nodeNames.Add(fullName, tree.Source.GetLocation(written.Span));
        return true;
    }

    /// <summary>Expands a node name with its file's prefix, or returns <see langword="null"/> if it can't be.</summary>
    private string? FullName(NameSyntax name)
    {
        if (name.IsMissing)
            return null;

        if (!name.Text.StartsWith('.'))
            return name.Text;

        if (tree.Root.Prefix is not { } prefix)
        {
            Report(DiagnosticCatalog.RelativeWithoutPrefix, name.Span, name.Text);
            return null;
        }

        return prefix.Name.IsMissing ? null : prefix.Name.Text + name.Text;
    }

    private void Declare<T>(Dictionary<string, T> table, T symbol, NameSyntax name, SymbolKind kind, string? shown = null)
        where T : Symbol
    {
        if (name.IsMissing)
            return;

        bool valid = CheckName(name, kind, shown);
        if (!table.TryGetValue(name.Text, out T? first))
            table.Add(name.Text, symbol);
        else if (valid)
            Report(DiagnosticCatalog.DuplicateDeclaration, name.Span, kind.Describe(), shown ?? name.Text, first.Location is { } location ? Where(location) : "built into Pibbles");
    }

    /// <summary>Reports a declared name that's reserved for its kind, or isn't ASCII. Returns whether the name is valid.</summary>
    private bool CheckName(NameSyntax name, SymbolKind kind, string? shown = null)
    {
        if (builtIn)
            return true;

        if (ReservedWords.UseOf(name.Text, kind) is { } use)
        {
            Report(DiagnosticCatalog.ReservedName, name.Span, shown ?? name.Text, kind.Describe(), use);
            return false;
        }

        int index = name.Text.AsSpan().IndexOfAnyExceptInRange('\0', '\x7F');
        if (index < 0)
            return true;

        Rune.DecodeFromUtf16(name.Text.AsSpan(index), out Rune character, out _);
        Report(DiagnosticCatalog.NonAsciiName, name.Span, shown ?? name.Text, character.ToString());
        return false;
    }

    private TypeSymbol ResolveType(NameSyntax name)
    {
        if (name.IsMissing)
            return TypeSymbol.Error;

        if ((TypeSymbol.BuiltIn.FirstOrDefault(type => type.Name == name.Text) ?? symbols.Enums.GetValueOrDefault(name.Text)) is { } found)
            return found;

        string? suggestion = Suggestions.Closest(name.Text, [.. TypeSymbol.BuiltIn.Select(type => type.Name), .. symbols.Enums.Keys]);
        ReportSuggesting(DiagnosticCatalog.UnknownType, name.Span, suggestion, name.Text);
        return TypeSymbol.Error;
    }

    /// <summary>The type of a variable with no type written, which its initial value gives, unless that value is a name.</summary>
    private TypeSymbol TypeOfValue(VariableDeclarationSyntax declaration)
    {
        switch (declaration.Value)
        {
            case NameExpressionSyntax name:
                string[] types = [.. TypesOfName(name.Name).Distinct()];
                string? written = types is [var type] ? $"@var ${declaration.Variable.Name}: {type} = {name.Name}" : null;
                ReportSuggesting(DiagnosticCatalog.UntypedVariable, name.Span, written, $"${declaration.Variable.Name}", name.Name);
                return TypeSymbol.Error;

            case UnaryExpressionSyntax { Operand: var operand }:
                return TypeOfLiteral(operand);

            default:
                return TypeOfLiteral(declaration.Value);
        }
    }

    private static TypeSymbol TypeOfLiteral(ExpressionSyntax literal) => literal switch
    {
        NumberLiteralSyntax => TypeSymbol.Number,
        DurationLiteralSyntax => TypeSymbol.Duration,
        StringLiteralSyntax => TypeSymbol.String,
        BooleanLiteralSyntax => TypeSymbol.Bool,
        _ => TypeSymbol.Error,
    };

    /// <summary>The types a bare name could be a value of: the enums it's a member of, <c>actor</c> and <c>node</c>.</summary>
    private IEnumerable<string> TypesOfName(string name)
    {
        if (name.Contains('.', StringComparison.Ordinal))
            return ["node"];

        IEnumerable<string> enums = symbols.Enums.Values.Where(@enum => @enum.Members.Any(member => member.Name == name)).Select(@enum => @enum.Name);
        return [.. enums, .. symbols.Actors.ContainsKey(name) ? ["actor"] : Array.Empty<string>(), .. nodeNames.ContainsKey(name) ? ["node"] : Array.Empty<string>()];
    }

    private SourceLocation? Location(TextSpan span) => builtIn ? null : tree.Source.GetLocation(span);

    /// <summary>Says where <paramref name="location"/> is, from the file being declared: <c>on line 3</c>, or <c>in story/cast.pib on line 3</c>.</summary>
    private string Where(SourceLocation location) =>
        location.Path == tree.Source.Path ? $"on line {location.Start.Line + 1}" : $"in {location.Path} on line {location.Start.Line + 1}";

    private void Report(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments) =>
        diagnostics.Add(descriptor.Create(tree.Source.GetLocation(span), arguments));

    /// <summary>Reports a diagnostic whose last argument is a suggestion for its help, leaving the help out when there's no suggestion.</summary>
    private void ReportSuggesting(DiagnosticDescriptor descriptor, TextSpan span, string? suggestion, params object?[] arguments)
    {
        Diagnostic diagnostic = descriptor.Create(tree.Source.GetLocation(span), [.. arguments, suggestion]);
        diagnostics.Add(suggestion is null ? diagnostic with { Help = null } : diagnostic);
    }
}
