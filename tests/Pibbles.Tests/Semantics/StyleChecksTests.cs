using Pibbles.Configuration;
using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

/// <summary>What the style rules' fixtures can't show: the fixes they suggest, their settings, and <c>// pibbles-ignore</c>.</summary>
public class StyleChecksTests
{
    public static TheoryData<string, string, string> Fixes { get; } = new()
    {
        { "@if $a == true\n    Hi.\n", "PIB5020", "Write `$a`." },
        { "@if $a != false\n    Hi.\n", "PIB5020", "Write `$a`." },
        { "@if $a == false\n    Hi.\n", "PIB5020", "Write `not $a`." },
        { "@if true != $a\n    Hi.\n", "PIB5020", "Write `not $a`." },
        { "@if ($a and $a) == false\n    Hi.\n", "PIB5020", "Write `not ($a and $a)`." },
        { "@set $n = $n - 2 * 3\n", "PIB5021", "Write `@set $n -= 2 * 3`." },
        { "Wait.{w 0.2}{w 300ms} Done.\n", "PIB5011", "Write one pause: `{w 0.5}`." },
        { "Wait.{w 0.1}{w 0.2} Done.\n", "PIB5011", "Write one pause: `{w 0.3}`." },
        { "Wait.{w}{p}Done.\n", "PIB5011", "Remove it." },
        { "Wait.{w 99999999999999999999999999999}{w 1} Done.\n", "PIB5011", "Write them as one pause." },
    };

    [Theory]
    [MemberData(nameof(Fixes))]
    public void Create_StyleProblem_SuggestsFix(string body, string code, string help)
    {
        var source = new SourceText("story.pib", $"@var $a = true\n@var $n = 0\n\n== a.b\n{body}");

        Diagnostic diagnostic = Assert.Single(Compilation.Create([source]).Diagnostics, diagnostic => diagnostic.Code == code);

        Assert.Equal(help, diagnostic.Help);
    }

    [Fact]
    public void Create_StyleProblemOnLineWithError_ReportsOnlyTheError()
    {
        var source = new SourceText("story.pib", "== a.b\n@if $missing == true\n    Hi.\n@return\n");

        IEnumerable<Diagnostic> diagnostics = Compilation.Create([source]).Diagnostics.Where(diagnostic => diagnostic.Code is not "PIB3010");

        Assert.Equal(["PIB2030", "PIB5014"], diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Theory]
    [InlineData("@actor HTTPServer\n    name: Server\n", "Write it as `http_server`, before anything outside the story uses the name.")]
    [InlineData("== Kitchen.FrontDoor\n", "Write it as `kitchen.front_door`, before anything outside the story uses the name.")]
    [InlineData("@var $hasKey2 = 0\n", "Write it as `$has_key2`, before anything outside the story uses the name.")]
    public void Create_NameNotInSnakeCase_SuggestsSnakeCase(string text, string help)
    {
        Diagnostic diagnostic = Assert.Single(Compilation.Create([new SourceText("story.pib", text)]).Diagnostics, diagnostic => diagnostic.Code is "PIB5031");

        Assert.Equal(help, diagnostic.Help);
    }

    [Fact]
    public void Create_RepeatedPoseOfActorWithDisplayName_NamesActorByDisplayName()
    {
        var source = new SourceText("story.pib", "@actor mira\n    name: Mira\n    poses: happy\n\n== a.b\nmira (happy): One.\nmira (happy): Two.\n");

        Diagnostic diagnostic = Assert.Single(Compilation.Create([source]).Diagnostics, diagnostic => diagnostic.Code is "PIB5010");

        Assert.Equal("This pose change does nothing, because Mira already has that pose.", diagnostic.Message);
    }

    [Theory]
    [InlineData(new[] { "pibbles_max_nesting=1" }, "== a.b\n@if $a\n    @if $a\n        Two deep.\n", "PIB5001")]
    [InlineData(new[] { "pibbles_max_message_length=10" }, "== a.b\nEleven char\n", "PIB5030")]
    [InlineData(new[] { "pibbles_max_option_length=3" }, "== a.b\n-> Four\n-> Ok\n", "PIB5030")]
    [InlineData(new[] { "pibbles_max_option_body=1" }, "== a.b\n-> Go\n    One.\n    Two.\n", "PIB5002")]
    [InlineData(new[] { "pibbles_min_repeated_lines=2" }, "== a.b\nTwice.\nTwice.\n", "PIB5003")]
    [InlineData(new[] { "pibbles_min_repeated_colors=1" }, "== a.b\n[color \"red\"]Once[/color].\n", "PIB5004")]
    [InlineData(new[] { "indent_size=2" }, "== a.b\n@if $a\n    Four.\n", "PIB5032")]
    [InlineData(new[] { "indent_style=tab" }, "== a.b\n@if $a\n    Spaces.\n", "PIB5032")]
    [InlineData(new[] { "indent_style=space" }, "== a.b\n@if $a\n\tA tab.\n", "PIB5032")]
    public void Create_WithStyleSetting_AppliesIt(string[] properties, string body, string code)
    {
        var source = new SourceText("story.pib", $"@var $a = true\n\n{body}");
        FileSettings settings = FileSettings.From(properties.Select(property => property.Split('=')).Select(parts => KeyValuePair.Create(parts[0], parts[1])));

        IEnumerable<Diagnostic> withDefaults = Compilation.Create([source]).Diagnostics;
        IEnumerable<Diagnostic> withSetting = Compilation.Create([source], new(new Dictionary<string, FileSettings> { ["story.pib"] = settings })).Diagnostics;

        Assert.DoesNotContain(withDefaults, diagnostic => diagnostic.Code == code);
        Assert.Contains(withSetting, diagnostic => diagnostic.Code == code);
    }

    public static TheoryData<string, string[]> Ignores { get; } = new()
    {
        { "== a.b\n// pibbles-ignore PIB5014\n@return\n", [] },
        { "== a.b\n// pibbles-ignore PIB5011 PIB5014\n// Another comment.\n@return\n", [] },
        { "== a.b\n// pibbles-ignore pib5014\n@return\n", [] },
        { "== a.b\n// pibbles-ignore PIB5014\n\n@return\n", ["PIB5014"] },
        { "== a.b\n// pibbles-ignore PIB5011\n@return\n", ["PIB5014"] },
        { "== a.b\n@set $n = 1 // pibbles-ignore PIB5014\n@return\n", ["PIB5014"] },
        { "// pibbles-ignore PIB5014\n== a.b\n@return\n== a.c\n@return\n", [] },
        { "== a.b\n// pibbles-ignore PIB2030\n@set $missing = 1\n", ["PIB2030"] },
    };

    [Theory]
    [MemberData(nameof(Ignores))]
    public void Create_PibblesIgnoreComment_SilencesOnlyStyleOnTheLineItCovers(string text, string[] codes)
    {
        var source = new SourceText("story.pib", $"@var $n = 0\n{text}");

        IEnumerable<Diagnostic> diagnostics = Compilation.Create([source]).Diagnostics.Where(diagnostic => diagnostic.Code is not ("PIB3010" or "PIB5040"));

        Assert.Equal(codes, diagnostics.Select(diagnostic => diagnostic.Code));
    }
}
