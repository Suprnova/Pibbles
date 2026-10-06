using Pibbles.Configuration;
using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Configuration;

public class FileSettingsTests
{
    /// <summary>A story with an unknown variable (PIB2030, binding) and a line with no ID (PIB3010, content).</summary>
    private readonly SourceText story = new("story.pib", "== a.b\n@set $missing = 1\nNarration.\n");

    public static TheoryData<string[], string[]> Severities { get; } = new()
    {
        { [], ["PIB2030 Error", "PIB3010 Warning"] },
        { ["pibbles_diagnostic.PIB3010.severity=error"], ["PIB2030 Error", "PIB3010 Error"] },
        { ["pibbles_diagnostic.category-binding.severity=warning"], ["PIB2030 Warning", "PIB3010 Warning"] },
        { ["pibbles_diagnostic.category-content.severity=none"], ["PIB2030 Error"] },
        { ["pibbles_diagnostic.pib3010.severity=None"], ["PIB2030 Error"] },
        { ["pibbles_diagnostic.PIB3010.severity=info", "pibbles_diagnostic.category-content.severity=none"], ["PIB2030 Error", "PIB3010 Info"] },
        { ["pibbles_diagnostic.category-content.severity=none", "pibbles_diagnostic.PIB3010.severity=hint"], ["PIB2030 Error", "PIB3010 Hint"] },
    };

    [Theory]
    [MemberData(nameof(Severities))]
    public void Create_WithSettings_GivesEachDiagnosticItsConfiguredSeverity(string[] properties, string[] expected)
    {
        var options = new CompilationOptions(new Dictionary<string, FileSettings> { ["story.pib"] = Settings(properties) });

        IEnumerable<Diagnostic> diagnostics = Compilation.Create([story], options).Diagnostics;

        Assert.Equal(expected, diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Severity}"));
    }

    [Fact]
    public void Create_SettingsForAnotherFile_LeavesThisOneAlone()
    {
        var options = new CompilationOptions(new Dictionary<string, FileSettings> { ["other.pib"] = Settings(["pibbles_diagnostic.category-content.severity=none"]) });

        Assert.Contains(Compilation.Create([story], options).Diagnostics, diagnostic => diagnostic.Code == "PIB3010");
    }

    [Theory]
    [InlineData("indent_style=tab", "Use only tabs in this file, since `indent_style` in `.editorconfig` asks for tabs. Most editors can convert the whole file for you.")]
    [InlineData("indent_style=Space", "Use only spaces in this file, since `indent_style` in `.editorconfig` asks for spaces. Most editors can convert the whole file for you.")]
    [InlineData("indent_size=4", "Use only spaces or only tabs in a file. Most editors can convert the whole file for you.")]
    public void Create_MixedIndentation_HelpFollowsIndentStyle(string property, string help)
    {
        var mixed = new SourceText("story.pib", "== a.b\n@if true\n    One. #id:a1b2c3\n@if true\n\tTwo. #id:d4e5f6\n");
        var options = new CompilationOptions(new Dictionary<string, FileSettings> { ["story.pib"] = Settings([property]) });

        Diagnostic diagnostic = Assert.Single(Compilation.Create([mixed], options).Diagnostics);

        Assert.Equal(("PIB1001", help), (diagnostic.Code, diagnostic.Help));
    }

    public static TheoryData<string> BadSettings { get; } =
    [
        "pibbles_diagnostic.PIB9999.severity=none",
        "pibbles_diagnostic.category-flavor.severity=none",
        "pibbles_diagnostic.PIB3010=none",
        "pibbles_diagnostic.PIB3010.severity=loud",
    ];

    [Theory]
    [MemberData(nameof(BadSettings))]
    public void From_BadPibblesSetting_ReportsAndIgnoresIt(string property)
    {
        FileSettings settings = Settings([property]);

        Assert.Single(settings.Problems);
        Assert.Empty(settings.Properties);
    }

    [Fact]
    public void From_OtherToolsSettings_KeepsThemAsWritten()
    {
        FileSettings settings = Settings(["Indent_Style=Space", "pibbles_max_nesting=4"]);

        Assert.Empty(settings.Problems);
        Assert.Equal(new Dictionary<string, string> { ["indent_style"] = "Space", ["pibbles_max_nesting"] = "4" }, settings.Properties);
    }

    private static FileSettings Settings(string[] properties) =>
        FileSettings.From(properties.Select(property => property.Split('=')).Select(parts => KeyValuePair.Create(parts[0], parts[1])));
}
