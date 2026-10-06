using System.Text.Json;
using System.Text.RegularExpressions;
using Pibbles.Cli.Commands;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Cli;

public sealed class CheckTests : IDisposable
{
    private readonly StringWriter output = new();
    private readonly SourceText clean = new("story/clean.pib", "@actor mira\n    name: Mira\n\n== kitchen.door\nmira: Locked. #id:k7qp2x\n");
    private readonly SourceText broken = new("story/broken.pib", "== kitchen.fridge\nI'm #winning today.\n@jump\n");

    public void Dispose() => output.Dispose();

    [Fact]
    public void Run_CleanStory_PrintsSummaryAndPasses()
    {
        int exitCode = Check.Run([clean], new(OutputFormat.Pretty), output);

        Assert.Equal((Check.Passed, "Checked 1 file: no problems.\n"), (exitCode, output.ToString().ReplaceLineEndings("\n")));
    }

    [Fact]
    public void Run_StoryWithErrors_Fails()
    {
        int exitCode = Check.Run([clean, broken], new(OutputFormat.Pretty), output);

        Assert.Equal(Check.Failed, exitCode);
        Assert.EndsWith("Checked 2 files: 2 errors.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_ManyMissingIds_ShowsThemAsOneEntryAfterOtherProblems()
    {
        SourceText rooms = new("story/rooms.pib", "== kitchen.cellar\nOne.\nTwo.\nThree.\nFour.\n@jump\n");
        SourceText hall = new("story/hall.pib", "== kitchen.hall\nFive.\nSix.\n");

        int exitCode = Check.Run([rooms, hall], new(OutputFormat.Pretty), output);

        Assert.Equal(Check.Failed, exitCode);
        Assert.EndsWith(
            "warning[PIB3010]: 6 lines have no `#id`.\n" +
            " --> story/rooms.pib: 4 lines\n" +
            " --> story/hall.pib: 2 lines\n" +
            "  |\n" +
            "  = help: Run `pibbles ids` to add them.\n" +
            "\n" +
            "Checked 2 files: 1 error and 6 warnings.\n",
            output.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(1, Regex.Count(output.ToString(), @"\[PIB3010\]"));
    }

    [Fact]
    public void Run_FewMissingIds_ShowsEachOne()
    {
        SourceText story = new("story/rooms.pib", "== kitchen.cellar\nOne.\nTwo.\nThree.\nFour.\nFive.\n");

        Check.Run([story], new(OutputFormat.Pretty), output);

        Assert.Equal(5, Regex.Count(output.ToString(), @"warning\[PIB3010\]: This line has no `#id`\."));
    }

    [Fact]
    public void Run_ManyMissingIdsInMSBuildFormat_ListsEachOne()
    {
        SourceText story = new("story/rooms.pib", "== kitchen.cellar\nOne.\nTwo.\nThree.\nFour.\nFive.\nSix.\n");

        Check.Run([story], new(OutputFormat.MSBuild), output);

        Assert.Equal(6, Regex.Count(output.ToString(), "warning PIB3010"));
    }

    [Fact]
    public void Run_MSBuildFormat_PrintsOneLinePerDiagnostic()
    {
        Check.Run([broken], new(OutputFormat.MSBuild), output);

        Assert.Equal(
            "story/broken.pib(2,5): error PIB1015: This text comes after a tag, but tags go at the end of the line.\n" +
            "story/broken.pib(3,6): error PIB1046: I expected a node name after `@jump`.\n",
            output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_JsonFormat_PrintsEveryField()
    {
        Check.Run([broken], new(OutputFormat.Json), output);

        JsonElement first = JsonDocument.Parse(output.ToString()).RootElement[0];
        Assert.Equal(
            ("story/broken.pib", 2, 5, 2, 13, "error", "PIB1015", "this starts a tag"),
            (first.GetProperty("path").GetString(), first.GetProperty("line").GetInt32(), first.GetProperty("column").GetInt32(),
             first.GetProperty("endLine").GetInt32(), first.GetProperty("endColumn").GetInt32(), first.GetProperty("severity").GetString(),
             first.GetProperty("code").GetString(), first.GetProperty("label").GetString()));
    }

    [Fact]
    public void Run_JsonFormatWithNoProblems_PrintsEmptyArray()
    {
        Check.Run([clean], new(OutputFormat.Json), output);

        Assert.Equal("[]", output.ToString().Trim());
    }

    public static TheoryData<DiagnosticSeverity[], bool, int> ExitCodes { get; } = new()
    {
        { [], false, Check.Passed },
        { [DiagnosticSeverity.Error], false, Check.Failed },
        { [DiagnosticSeverity.Warning], false, Check.Passed },
        { [DiagnosticSeverity.Warning], true, Check.Failed },
        { [DiagnosticSeverity.Info, DiagnosticSeverity.Hint], true, Check.Passed },
    };

    [Theory]
    [MemberData(nameof(ExitCodes))]
    public void ExitCode_BySeverity_FailsOnErrorsAndOptionallyWarnings(DiagnosticSeverity[] severities, bool warnAsError, int expected)
    {
        SourceLocation location = clean.GetLocation(new(0, 0));
        Diagnostic[] diagnostics = [.. severities.Select(severity => new Diagnostic("PIB0000", severity, location, "message", null, null))];

        Assert.Equal(expected, Check.ExitCode(diagnostics, warnAsError));
    }
}
