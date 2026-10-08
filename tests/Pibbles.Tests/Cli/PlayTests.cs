using System.Text.RegularExpressions;
using Pibbles.Cli.Commands;
using Pibbles.Cli.Output;
using Pibbles.Cli.Transcripts;

namespace Pibbles.Tests.Cli;

public sealed partial class PlayTests : IDisposable
{
    private const string Story = """
        @actor mira
            name: Mira
        @var $key = false
        @var $coins = 0
        @function price(item: string) -> number
        @function lucky() -> bool

        == t.n
        mira: Hello. #id:l1
        -> Open  @if $key #id:o1
            mira: Opened. #id:l2
        -> Buy #id:o2
            mira: That's {price("rope")}. #id:l3
        -> Leave #id:o3
            mira: Bye, {$coins}. #id:l4

        == t.other
        mira: Elsewhere. #id:l5
        """;

    private static readonly string Kitchen = Path.Combine(RepositoryRoot.Path, "samples", "kitchen");

    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("pibbles-");
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public PlayTests()
    {
        Directory.CreateDirectory(Path.Combine(root.FullName, "story"));
        File.WriteAllText(Path.Combine(root.FullName, "story", "story.pib"), Story);
    }

    public static TheoryData<string> KitchenTranscripts { get; } = [.. TranscriptFile.Find(Path.Combine(Kitchen, "transcripts")).Select(path => Path.GetFileName(path))];

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        root.Delete(recursive: true);
    }

    [Theory]
    [MemberData(nameof(KitchenTranscripts))]
    public void Play_KitchenTranscript_KitchenSamplePlaysThroughAllItsBranchesInTheTerminal(string file)
    {
        string path = Path.Combine(Kitchen, "transcripts", file);

        int exitCode = Run(new(Kitchen, Script: path));

        Assert.Equal((Play.Played, TranscriptFile.Normalize(File.ReadAllText(path))), (exitCode, output.ToString()));
    }

    [Fact]
    public void Play_Script_PrintsWhatTheTranscriptPlayerPrints()
    {
        string script = Write("run.script", "start t.n\nstub price = 3\nstub lucky = true\n> #o2\n");

        int exitCode = Run(new(".", Script: script));

        Assert.Equal(Play.Played, exitCode);
        Assert.Equal(
            "start t.n\nstub price = 3\nstub lucky = true\n\n  mira: Hello.  #l1\n  choice\n    1. Open  #o1 (unavailable)\n    2. Buy  #o2\n    3. Leave  #o3\n> #o2\n  mira: That's 3.  #l3\n  end\n",
            output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void Play_SetFlag_WinsOverTheScriptsOwnAndIsPrintedAfterIt()
    {
        string script = Write("run.script", "start t.n\nset $key = false\nstub price = 3\nstub lucky = true\n> #o1\n");

        int exitCode = Run(new(".", Script: script, Sets: ["$key=true"]));

        Assert.Equal(Play.Played, exitCode);
        Assert.StartsWith("start t.n\nset $key = false\nstub price = 3\nstub lucky = true\nset $key=true\n\n", output.ToString());
        Assert.Contains("  mira: Opened.  #l2\n", output.ToString());
    }

    [Fact]
    public void Play_StubFlags_WinOverTheStubsFileWhichWinsOverTheScript()
    {
        string script = Write("run.script", "start t.n\nstub price = 1\nstub lucky = true\n> #o2\n");
        string stubs = Write("stubs.txt", "stub price = 2\n\nstub price(\"rope\") = 5\n");

        int exitCode = Run(new(".", Script: script, StubsFile: stubs, Stubs: ["price(\"rope\")=7"]));

        Assert.Equal(Play.Played, exitCode);
        Assert.Contains("  mira: That's 7.  #l3\n", output.ToString());
    }

    [Fact]
    public void Play_StartFlag_ReplacesTheScriptsStart()
    {
        string script = Write("run.script", "start t.n\nstub price = 1\nstub lucky = true\n");

        int exitCode = Run(new(".", Script: script, Start: "t.other"));

        Assert.Equal((Play.Played, "start t.other\nstub price = 1\nstub lucky = true\n\n  mira: Elsewhere.  #l5\n  end\n"), (exitCode, output.ToString()));
    }

    [Fact]
    public void Play_StartFlagAndScriptWithoutStart_StartsThere()
    {
        string script = Write("run.script", "stub price = 1\nstub lucky = true\n");

        int exitCode = Run(new(".", Script: script, Start: "t.other"));

        Assert.Equal(Play.Played, exitCode);
        Assert.StartsWith("start t.other\n", output.ToString());
    }

    [Theory]
    [InlineData("start t.n\nstub price = 1\n", "`lucky`")]
    [InlineData("start t.n\nstub price(\"key\") = 1\nstub lucky = true\n> #o2\n", "price(\"rope\")")]
    [InlineData("start t.n\nstub price = 1\nstub lucky = true\n", "no answer")]
    [InlineData("start t.n\nset key = true\nstub price = 1\nstub lucky = true\n", "set $name = value")]
    [InlineData("start t.nope\nstub price = 1\nstub lucky = true\n", "t.nope")]
    public void Play_FailingScript_SaysWhyAndFails(string text, string message)
    {
        string script = Write("run.script", text);

        int exitCode = Run(new(".", Script: script));

        Assert.Equal(Play.Failed, exitCode);
        Assert.Contains(message, error.ToString());
    }

    [Fact]
    public void Play_StubsFileWithOtherLines_Fails()
    {
        string script = Write("run.script", "start t.n\n");
        string stubs = Write("stubs.txt", "set $key = true\n");

        int exitCode = Run(new(".", Script: script, StubsFile: stubs));

        Assert.Equal(Play.Failed, exitCode);
        Assert.Contains("only `stub` lines", error.ToString());
    }

    [Fact]
    public void Play_StoryWithErrors_PrintsThemAndFails()
    {
        File.WriteAllText(Path.Combine(root.FullName, "story", "broken.pib"), "== t.broken\n@jump\n");

        int exitCode = Run(new(".", Start: "t.n"));

        Assert.Equal((Play.Failed, ""), (exitCode, output.ToString()));
        Assert.Contains("error[PIB", error.ToString());
        Assert.EndsWith("Checked 2 files: 1 error.\n", error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Play_StoryWithWarnings_CountsThemOnTheErrorStream()
    {
        File.WriteAllText(Path.Combine(root.FullName, "story", "loose.pib"), "== t.loose\nNo ID.\nNor here.\n");
        string script = Write("run.script", "start t.other\nstub price = 1\nstub lucky = true\n");

        int exitCode = Run(new(".", Script: script));

        Assert.Equal(Play.Played, exitCode);
        Assert.Equal("2 warnings; run `pibbles check` to see them.\n", error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Play_UnreadableScript_CouldNotRun()
    {
        Assert.Equal(Play.CouldNotRun, Run(new(".", Script: "missing.script")));
        Assert.Contains("missing.script", error.ToString());
    }

    [Fact]
    public void Play_NoStory_CouldNotRun()
    {
        Directory.Delete(Path.Combine(root.FullName, "story"), recursive: true);

        Assert.Equal(Play.CouldNotRun, Run(new(".", Start: "t.n")));
    }

    [Fact]
    public void Play_NeitherStartNorScript_CouldNotRun()
    {
        Assert.Equal(Play.CouldNotRun, Run(new(".")));
        Assert.Contains("--start", error.ToString());
    }

    [Fact]
    public void Play_Interactive_AnswersByNumberAndIdAsksAgainForInvalidAnswersAndPromptsForStubs()
    {
        var input = new StringReader("\n9\n1\n2\n4\n\n\n");

        int exitCode = Run(new(".", Start: "t.n", Stubs: ["lucky=true"]), input);

        string shown = output.ToString();
        Assert.Equal(Play.Played, exitCode);
        Assert.Contains("The answer `> 9` isn't one of the options on offer", shown);
        Assert.Contains("The answer `> 1` is an option that isn't available.", shown);
        Assert.Contains("> #o2\n", shown);
        Assert.Contains("Nothing stubs `price(\"rope\")`. What does it return? (number): ", shown);
        Assert.Contains("  mira: That's 4.  #l3\n  end\n", shown);
    }

    [Fact]
    public void Play_InteractiveStubValueOfTheWrongType_AsksAgain()
    {
        var input = new StringReader("\n#o2\nlots\n2.5\n\n");

        int exitCode = Run(new(".", Start: "t.n", Stubs: ["lucky=true"]), input);

        Assert.Equal(Play.Played, exitCode);
        Assert.Contains("`lots` isn't a number", output.ToString());
        Assert.Contains("  mira: That's 2.5.  #l3\n", output.ToString());
    }

    [Fact]
    public void Play_InteractiveWithoutPause_PrintsStraightToTheChoice()
    {
        var input = new StringReader("#o3\n");

        int exitCode = Run(new(".", Start: "t.n", Pause: false), input);

        Assert.Equal(Play.Played, exitCode);
        Assert.Contains("> #o3\n  mira: Bye, 0.  #l4\n  end\n", output.ToString());
    }

    [Fact]
    public void Play_InteractiveEndOfInputMidSession_StopsCleanlyAndRecordsWhatHappened()
    {
        string record = Path.Combine(root.FullName, "session.transcript");

        int exitCode = Run(new(".", Start: "t.n", Record: record), new StringReader("\n"));

        Assert.Equal(Play.Played, exitCode);
        Assert.Contains("End of input, so play stopped.", error.ToString());
        Assert.Equal(
            "start t.n\nstub price = 0\nstub lucky = false\n\n  mira: Hello.  #l1\n  choice\n    1. Open  #o1 (unavailable)\n    2. Buy  #o2\n    3. Leave  #o3\n",
            File.ReadAllText(record));
    }

    [Fact]
    public void Play_InteractiveRecord_ReplaysIdenticallyWithScript()
    {
        string record = Path.Combine(root.FullName, "session.transcript");
        Run(new(".", Start: "t.n", Sets: ["$coins=3"], Record: record), new StringReader("\n2\n6\n\n"));
        var replayed = new StringWriter();

        int exitCode = Run(new(".", Script: record), into: replayed);

        Assert.Equal((Play.Played, File.ReadAllText(record)), (exitCode, replayed.ToString()));
        Assert.StartsWith("start t.n\nset $coins=3\nstub price(\"rope\") = 6\nstub lucky = false\n\n", replayed.ToString());
    }

    [Fact]
    public void Play_InteractiveInterrupted_SaysSoAndRecordsWhatHappened()
    {
        string record = Path.Combine(root.FullName, "session.transcript");
        using var interrupted = new CancellationTokenSource();
        var input = new InterruptingReader(interrupted);

        int exitCode = Play.Run(new(".", Start: "t.n", Record: record), root.FullName, input, output, error, interrupted.Token);

        Assert.Equal(Play.Played, exitCode);
        Assert.Contains("Interrupted, so play stopped.", error.ToString());
        Assert.DoesNotContain("End of input, so play stopped.", error.ToString());
        Assert.EndsWith("  choice\n    1. Open  #o1 (unavailable)\n    2. Buy  #o2\n    3. Leave  #o3\n", File.ReadAllText(record));
    }

    [Fact]
    public void Play_InteractiveWithColor_ColorsTheSameText()
    {
        Run(new(".", Start: "t.n", Pause: false, Color: true), new StringReader("#o3\n"));

        string plain = AnsiCode().Replace(output.ToString(), "");
        Assert.Contains("\e[", output.ToString());
        Assert.Contains("  mira: Bye, 0.  #l4\n", plain);
    }

    [Theory]
    [InlineData("start t.n")]
    [InlineData("> #o2")]
    [InlineData("  mira: Locked.⟨w⟩ Of course ⟨@sfx thud⟩it's locked. #thought  #k7qp2x")]
    [InlineData("    2. Use the key #show_disabled  #m3xw9a (unavailable) (chosen)")]
    [InlineData("  warning DivisionByZero (story.pib line 3): Dividing by zero gave 0.")]
    [InlineData("  @show mira center waits")]
    [InlineData("  $key = true")]
    public void Paint_AnyTranscriptLine_ChangesNoCharacter(string line)
    {
        string painted = TranscriptColors.Paint(line);

        Assert.NotEqual(line, painted);
        Assert.Equal(line, AnsiCode().Replace(painted, ""));
    }

    /// <summary>Plays in the test's project folder, reading <paramref name="input"/> and printing to <paramref name="into"/>, or to <see cref="output"/>.</summary>
    private int Run(PlayOptions options, TextReader? input = null, TextWriter? into = null) =>
        Play.Run(options, root.FullName, input ?? TextReader.Null, into ?? output, error, TestContext.Current.CancellationToken);

    private string Write(string name, string text)
    {
        string path = Path.Combine(root.FullName, name);
        File.WriteAllText(path, text);
        return path;
    }

    [GeneratedRegex(@"\e\[[0-9;]*m")]
    private static partial Regex AnsiCode();

    /// <summary>Presses Enter after the first line, then Ctrl+C at the choice, then reaches the end of input.</summary>
    private sealed class InterruptingReader(CancellationTokenSource interrupt) : TextReader
    {
        private int reads;

        public override string? ReadLine()
        {
            if (reads++ == 0)
                return "";

            interrupt.Cancel();
            return null;
        }
    }
}
