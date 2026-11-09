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
        Assert.Equal(["b", "i", "u", "s", "color"], compilation.Symbols.Markup.Keys);
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
    public void Create_NamesInAnyScript_DeclareAndResolveAsWritten()
    {
        var compilation = Compile("""
            @actor zoë
                name: Zoë
            @actor 田中
                name: 田中
            @enum mood: calm, 快乐
            @tag café
            @var $café: mood = 快乐

            == kitchen.café
            田中: こんにちは。 #id:a00001 #café
            zoë: Salut. #id:a00002
            @if $café == 快乐
                @jump kitchen.café

            """);

        Assert.Empty(compilation.Diagnostics);
        Assert.Contains("zoë", compilation.Symbols.Actors.Keys);
        Assert.Contains("田中", compilation.Symbols.Actors.Keys);
        Assert.Equal(["calm", "快乐"], compilation.Symbols.Enums["mood"].Members.Select(member => member.Name));
        Assert.Contains("café", compilation.Symbols.Tags.Keys);
        Assert.Contains("café", compilation.Symbols.Variables.Keys);
        Assert.Contains("kitchen.café", compilation.Symbols.Nodes.Keys);
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
    public void Create_DisplayNameWithBracketBraceAndBackslash_KeepsThemAsText()
    {
        var compilation = Compile("@actor mira\n    name: Mira [the {brave}] \\ #1\n");

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(@"Mira [the {brave}] \ #1", compilation.Symbols.Actors["mira"].DisplayName);
    }

    [Fact]
    public void Create_SeveralFiles_OrdersDiagnosticsByFileThenPosition()
    {
        var compilation = WithoutStyle.Compile(
            new("story/b.pib", "@enum room: a\n@enum room: b\n@var $x = @\n"),
            new("story/a.pib", "@enum spot: a, a\n@command if()\n"));

        Assert.Equal(["PIB1402", "PIB1103", "PIB1403", "PIB1404"], compilation.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void Create_RelativeVariable_IsDeclaredUnderItsFullName()
    {
        var compilation = Compile("@prefix kitchen\n@var $.attempts = 0\n");

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(["kitchen.attempts"], compilation.Symbols.Variables.Keys);
    }

    [Fact]
    public void Create_RelativeVariablesUnderDifferentPrefixes_AreDifferentVariables()
    {
        var compilation = WithoutStyle.Compile(
            new("story/kitchen.pib", "@prefix kitchen\n@var $.attempts = 0\n"),
            new("story/cellar.pib", "@prefix cellar\n@var $.attempts = 0\n"));

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(["kitchen.attempts", "cellar.attempts"], compilation.Symbols.Variables.Keys);
    }

    [Fact]
    public void Create_RelativeVariablesUnderTheSamePrefix_AreOneVariable()
    {
        var compilation = WithoutStyle.Compile(
            new("story/kitchen.pib", "@prefix kitchen\n@var $.attempts = 0\n"),
            new("story/pantry.pib", "@prefix kitchen\n@var $.attempts = 0\n"));

        Diagnostic duplicate = Assert.Single(compilation.Diagnostics);
        Assert.Equal(("PIB1402", "There's already a variable called `$kitchen.attempts`, in story/kitchen.pib on line 2."), (duplicate.Code, duplicate.Message));
    }

    [Theory]
    [InlineData("@var $attempts = 0\n@var $attempts = 0\n", "Two `@var`s are two variables, so each needs its own name. Give this one a name that says whose it is.")]
    [InlineData("@prefix kitchen\n@var $attempts = 0\n@var $attempts = 0\n", "Two `@var`s are two variables, so each needs its own name. Write this one as `$.attempts`, which is `$kitchen.attempts`, or give it a name that says whose it is.")]
    [InlineData("@prefix kitchen\n@var $.attempts = 0\n@var $.attempts = 0\n", "Two `@var`s are two variables, so each needs its own name. Give this one a name that says whose it is.")]
    [InlineData("@prefix kitchen\n@var $kitchen.attempts = 0\n@var $kitchen.attempts = 0\n", "Two `@var`s are two variables, so each needs its own name. Give this one a name that says whose it is.")]
    [InlineData("@enum room: kitchen\n@enum room: cellar\n", "Give one of them another name, or remove one.")]
    public void Create_DuplicateDeclaration_HelpSuitsTheKind(string source, string help)
    {
        var compilation = Compile(source);

        Assert.Equal(help, Assert.Single(compilation.Diagnostics).Help);
    }

    [Fact]
    public void Create_FullVariableName_IsReachedFromAFileWithAnotherPrefix()
    {
        var compilation = WithoutStyle.Compile(
            new("story/kitchen.pib", "@prefix kitchen\n@var $.lights_on = false\n"),
            new("story/cellar.pib", "@prefix cellar\n\n== .breaker\n@set $kitchen.lights_on = true\n"));

        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_VariableDeclaredBetweenNodes_IsUsedAboveBelowAndInAnotherFile()
    {
        var compilation = WithoutStyle.Compile(
            new("story/kitchen.pib", "@prefix kitchen\n\n== .door\n@set $.door_open = true\n@jump .window\n\n@var $.door_open = false\n== .window\n@set $.door_open = false\n"),
            new("story/cellar.pib", "@prefix cellar\n\n== .stairs\n@set $kitchen.door_open = true\n"));

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(["kitchen.door_open"], compilation.Symbols.Variables.Keys);
    }

    [Fact]
    public void Create_VariableDeclaredAtTheTopAndBetweenNodes_IsReportedOnce()
    {
        var compilation = WithoutStyle.Compile(new SourceText("story/kitchen.pib", "@var $open = false\n== kitchen.door\n@set $open = true\n@var $open = true\n== kitchen.window\n"));

        Assert.Equal("PIB1402", Assert.Single(compilation.Diagnostics).Code);
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

    [Fact]
    public void Create_VariableDeclaredWithoutDollar_ReportsOnlyTheMissingDollar()
    {
        Compilation compilation = Compile("@var door_open = false\n== kitchen.door\n@if $door_open\n    @end\n");

        Assert.Equal("PIB2301", Assert.Single(compilation.Diagnostics).Code);
    }

    [Theory]
    [MemberData(nameof(UntypedVariableHelp))]
    public void Create_VariableStartingAsName_SuggestsTypeOnlyWhenThereIsOne(string text, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal(("PIB2304", help), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("postion", "Did you mean `position`?")]
    [InlineData("numbr", "Did you mean `number`?")]
    [InlineData("feeling", null)]
    public void Create_UnknownType_SuggestsClosestType(string type, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Compile($"@enum position: left\n@command show(at: {type})\n").Diagnostics);

        Assert.Equal(("PIB1406", help), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("clu", "Did you mean `clue`?")]
    [InlineData("e", null)]
    public void Create_UnknownMarkup_SuggestsClosestMarkupSharingALetter(string markup, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Compile($"@markup clue\n\n== kitchen.door\n[{markup}]the key[/{markup}]\n").Diagnostics, d => d.Code is "PIB3104");

        Assert.Equal(help, diagnostic.Help);
    }

    private static Compilation Compile(string text) => WithoutStyle.Compile(new SourceText("story.pib", text));
}
