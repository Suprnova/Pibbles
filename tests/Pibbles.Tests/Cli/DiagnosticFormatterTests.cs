using Pibbles.Cli.Output;
using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Cli;

public sealed class DiagnosticFormatterTests : IDisposable
{
    private readonly StringWriter output = new();

    public void Dispose() => output.Dispose();

    [Fact]
    public void WritePretty_TextAfterTag_MatchesDocumentedExample()
    {
        string text = "== kitchen.door\n" + string.Concat(Enumerable.Repeat("Narration.\n", 10)) + "mira: I'm #winning today.\n";
        var source = new SourceText("story/kitchen.pib", text);
        var cast = new SourceText("story/cast.pib", "@actor mira\n    name: Mira\n");
        Diagnostic diagnostic = Assert.Single(Compilation.Create([cast, source]).Diagnostics, diagnostic => diagnostic.Code is "PIB1015");

        DiagnosticFormatter.WritePretty(output, source, diagnostic, color: false);

        Assert.Equal(DocumentedExample(), output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WritePretty_ZeroLengthSpan_MarksOneCaret()
    {
        var source = new SourceText("story.pib", "@jump");
        Diagnostic diagnostic = Problem(source, new(5, 0), label: null, help: null);

        DiagnosticFormatter.WritePretty(output, source, diagnostic, color: false);

        Assert.Equal("error[PIB0000]: message\n --> story.pib:1:6\n  |\n1 | @jump\n  |      ^\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WritePretty_LineWithTabs_ShowsTabsAsFourSpacesAndAlignsCarets()
    {
        var source = new SourceText("story.pib", "\tmira: {if $x}");
        Diagnostic diagnostic = Problem(source, new(7, 3), label: null, help: null);

        DiagnosticFormatter.WritePretty(output, source, diagnostic, color: false);

        Assert.EndsWith("1 |     mira: {if $x}\n  |           ^^^\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WritePretty_SpanPastEndOfLine_StopsCaretsAtLineEnd()
    {
        var source = new SourceText("story.pib", "mira: [b]Bold\nrex: Hm.");
        Diagnostic diagnostic = Problem(source, new(6, 12), label: null, help: null);

        DiagnosticFormatter.WritePretty(output, source, diagnostic, color: false);

        Assert.EndsWith("  |       ^^^^^^^\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WritePretty_Color_PaintsOnlyWhenAsked(bool color)
    {
        var source = new SourceText("story.pib", "@jump");

        DiagnosticFormatter.WritePretty(output, source, Problem(source, new(5, 0), "label", "help"), color);

        Assert.Equal(color, output.ToString().Contains('\u001b', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, new DiagnosticSeverity[0], "Checked 1 file: no problems.")]
    [InlineData(3, new[] { DiagnosticSeverity.Error, DiagnosticSeverity.Error, DiagnosticSeverity.Warning }, "Checked 3 files: 2 errors and 1 warning.")]
    [InlineData(2, new[] { DiagnosticSeverity.Error, DiagnosticSeverity.Warning, DiagnosticSeverity.Warning, DiagnosticSeverity.Hint }, "Checked 2 files: 1 error, 2 warnings and 1 hint.")]
    [InlineData(1, new[] { DiagnosticSeverity.Info }, "Checked 1 file: 1 note.")]
    public void Summary_CountsEachSeverity(int files, DiagnosticSeverity[] severities, string expected)
    {
        var source = new SourceText("story.pib", "");
        Diagnostic[] diagnostics = [.. severities.Select(severity => Problem(source, new(0, 0), null, null) with { Severity = severity })];

        Assert.Equal(expected, DiagnosticFormatter.Summary(files, diagnostics));
    }

    [Fact]
    public void WritePretty_WithColor_HighlightsProblemAndPaintsCodeAndFix()
    {
        var source = new SourceText("story.pib", "== kitchen.door\nmira: I'm #winning today.\n");
        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        DiagnosticFormatter.WritePretty(output, source, diagnostic, color: true);

        string text = output.ToString();
        Assert.Contains("mira: I'm \e[1;31m#winning\e[0m today.", text);
        Assert.Contains("\e[36m#winning\e[39m is part of", text);
        Assert.Contains("\e[32mmira: I'm \\#winning today.\e[0m", text);
    }

    [Fact]
    public void WritePretty_WithColor_ShowsBackticksInSourceVerbatim()
    {
        var source = new SourceText("story.pib", "mira: Use `this` [b]here");
        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        DiagnosticFormatter.WritePretty(output, source, diagnostic, color: true);

        Assert.Contains("mira: Use `this` ", output.ToString());
    }

    private static Diagnostic Problem(SourceText source, TextSpan span, string? label, string? help) =>
        new("PIB0000", DiagnosticSeverity.Error, source.GetLocation(span), "message", label, help);

    /// <summary>The example under "How diagnostics read" in <c>docs/syntax.md</c>, which this rendering must match.</summary>
    private static string DocumentedExample()
    {
        string syntax = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "syntax.md")).ReplaceLineEndings("\n");
        string section = syntax[syntax.IndexOf("### How diagnostics read", StringComparison.Ordinal)..];
        int start = section.IndexOf("```text\n", StringComparison.Ordinal) + "```text\n".Length;
        return section[start..section.IndexOf("```\n", start, StringComparison.Ordinal)];
    }
}
