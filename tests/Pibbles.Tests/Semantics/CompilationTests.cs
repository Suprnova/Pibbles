using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public class CompilationTests
{
    [Fact]
    public void Prelude_Parses_ReportsNothing() => Assert.Empty(Prelude.Tree.Diagnostics);

    [Fact]
    public void Create_EmptyStory_DeclaresPreludeOnly()
    {
        var compilation = Compilation.Create([]);

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(["b", "i", "u", "s", "color", "speed"], compilation.Symbols.Markup.Keys);
        Assert.Equal(["visits"], compilation.Symbols.Functions.Keys);
        Assert.Same(TypeSymbol.String, Assert.Single(compilation.Symbols.Markup["color"].Parameters).Type);
        Assert.Null(compilation.Symbols.Markup["b"].Location);
    }

    [Fact]
    public void Create_RelativeNodeNames_ExpandWithFilePrefix()
    {
        var compilation = Compile("@prefix kitchen\n\n== .door #was:.front_door\n");

        NodeSymbol door = compilation.Symbols.Nodes["kitchen.door"];
        Assert.Same(door, compilation.Symbols.Aliases["kitchen.front_door"]);
        Assert.Equal(["kitchen.front_door"], door.Aliases);
    }

    [Fact]
    public void Create_DuplicateInSameFile_SaysWhichLine()
    {
        var compilation = Compile("@enum room: kitchen\n@enum room: cellar\n");

        Assert.Equal("There's already an enum called `room`, on line 1.", Assert.Single(compilation.Diagnostics).Message);
    }

    [Fact]
    public void Create_DuplicateInAnotherFile_SaysWhichFileAndLine()
    {
        var compilation = Compilation.Create([new("story/cast.pib", "\n== kitchen.door\n"), new("story/rooms.pib", "== kitchen.door\n")]);

        Diagnostic duplicate = Assert.Single(compilation.Diagnostics);
        Assert.Equal(("story/rooms.pib", "There's already a node called `kitchen.door`, in story/cast.pib on line 2."), (duplicate.Location.Path, duplicate.Message));
    }

    [Fact]
    public void Create_DuplicateDeclaration_KeepsFirst()
    {
        var compilation = Compile("@actor mira\n    name: Mira\n@actor mira\n    name: Someone else\n");

        Assert.Equal("Mira", compilation.Symbols.Actors["mira"].DisplayName);
    }

    [Fact]
    public void Create_SeveralFiles_OrdersDiagnosticsByFileThenPosition()
    {
        var compilation = WithoutStyle.Compile(
            new("story/b.pib", "@enum room: a\n@enum room: b\n@var $x = @\n"),
            new("story/a.pib", "@enum spot: a, a\n@tag café\n"));

        Assert.Equal(["PIB2060", "PIB1040", "PIB2061", "PIB2063"], compilation.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    public static TheoryData<string, string> LiteralTypes { get; } = new()
    {
        { "3", "number" },
        { "-0.5", "number" },
        { "300ms", "duration" },
        { "-1s", "duration" },
        { "\"Sam\"", "string" },
        { "false", "bool" },
    };

    [Theory]
    [MemberData(nameof(LiteralTypes))]
    public void Create_VariableWithoutType_TakesTypeOfInitialValue(string value, string type) =>
        Assert.Equal(type, Compile($"@var $x = {value}\n").Symbols.Variables["x"].Type.Name);

    [Fact]
    public void Create_VariableWithWrittenType_UsesThatType()
    {
        var compilation = Compile("@enum position: left, right\n@var $where: position = left\n");

        Assert.Same(compilation.Symbols.Enums["position"], compilation.Symbols.Variables["where"].Type);
    }

    public static TheoryData<string, string?> UntypedVariableHelp { get; } = new()
    {
        { "@enum position: left, right\n@var $where = left\n", "Write the type after the variable: `@var $where: position = left`." },
        { "@actor left\n    name: Lefty\n@var $where = left\n", "Write the type after the variable: `@var $where: actor = left`." },
        { "@var $where = .left\n", "Write the type after the variable: `@var $where: node = .left`." },
        { "@enum position: left\n@enum side: left\n@var $where = left\n", null },
        { "@var $where = left\n", null },
    };

    [Theory]
    [MemberData(nameof(UntypedVariableHelp))]
    public void Create_VariableStartingAsName_SuggestsTypeOnlyWhenThereIsOne(string text, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal(("PIB2067", help), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("postion", "Did you mean `position`?")]
    [InlineData("numbr", "Did you mean `number`?")]
    [InlineData("feeling", null)]
    public void Create_UnknownType_SuggestsClosestType(string type, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Compile($"@enum position: left\n@command show(at: {type})\n").Diagnostics);

        Assert.Equal(("PIB2064", help), (diagnostic.Code, diagnostic.Help));
    }

    private static Compilation Compile(string text) => WithoutStyle.Compile(new SourceText("story.pib", text));
}
