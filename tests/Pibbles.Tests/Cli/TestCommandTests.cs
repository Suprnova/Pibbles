using Pibbles.Cli.Commands;

namespace Pibbles.Tests.Cli;

public sealed class TestCommandTests : IDisposable
{
    private const string Transcript = "start t.n\n\n  mira: Hello.  #l1\n  end\n";

    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("pibbles-");
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public TestCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(root.FullName, "story"));
        Directory.CreateDirectory(Path.Combine(root.FullName, "transcripts", "nested"));
        File.WriteAllText(Path.Combine(root.FullName, "story", "story.pib"), "@actor mira\n    name: Mira\n\n== t.n\nmira: Hello. #id:l1\n");
    }

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        root.Delete(recursive: true);
    }

    [Fact]
    public void Run_MatchingTranscriptsAtAnyDepth_Pass()
    {
        WriteTranscript("hello.transcript", Transcript);
        WriteTranscript("nested/crlf.transcript", Transcript.Replace("\n", "\r\n", StringComparison.Ordinal));

        int exitCode = Test.Run(".", root.FullName, update: false, output, error, color: false);

        Assert.Equal((Test.Passed, "Ran 2 transcripts: 2 passed, 0 failed.\n"), (exitCode, output.ToString().ReplaceLineEndings("\n")));
    }

    [Fact]
    public void Run_TranscriptThatNoLongerMatches_ShowsWhereItDiffersAndFails()
    {
        WriteTranscript("hello.transcript", "start t.n\n\n  mira: Hi.  #l1\n  end\n");

        int exitCode = Test.Run(".", root.FullName, update: false, output, error, color: false);

        Assert.Equal(Test.Failed, exitCode);
        Assert.Equal(
            "FAILED transcripts/hello.transcript differs from line 3:\n" +
            "  -   mira: Hi.  #l1\n  -   end\n  - \n" +
            "  +   mira: Hello.  #l1\n  +   end\n  + \n" +
            "Ran 1 transcript: 0 passed, 1 failed.\n",
            output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_TranscriptWhoseScriptFails_SaysWhyAndFails()
    {
        WriteTranscript("hello.transcript", "start t.nope\n");

        int exitCode = Test.Run(".", root.FullName, update: true, output, error, color: false);

        Assert.Equal(Test.Failed, exitCode);
        Assert.StartsWith("FAILED transcripts/hello.transcript: The story has no node called `t.nope`.", output.ToString());
    }

    [Fact]
    public void Run_Update_RewritesOnlyFailingTranscriptsKeepingTheirLineEndings()
    {
        WriteTranscript("good.transcript", Transcript);
        WriteTranscript("bad.transcript", "start t.n\r\n\r\n  mira: Hi.  #l1\r\n");
        DateTime untouched = File.GetLastWriteTimeUtc(Path.Combine(root.FullName, "transcripts", "good.transcript"));

        int exitCode = Test.Run(".", root.FullName, update: true, output, error, color: false);

        Assert.Equal(Test.Passed, exitCode);
        Assert.Equal(Transcript.Replace("\n", "\r\n", StringComparison.Ordinal), File.ReadAllText(Path.Combine(root.FullName, "transcripts", "bad.transcript")));
        Assert.Equal(untouched, File.GetLastWriteTimeUtc(Path.Combine(root.FullName, "transcripts", "good.transcript")));
        Assert.Equal("UPDATED transcripts/bad.transcript\nRan 2 transcripts: 1 passed, 1 updated.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_NoTranscriptsFolder_CouldNotRun()
    {
        Directory.Delete(Path.Combine(root.FullName, "transcripts"), recursive: true);

        Assert.Equal(Test.CouldNotRun, Test.Run(".", root.FullName, update: false, output, error, color: false));
        Assert.Contains("`transcripts` folder", error.ToString());
    }

    [Fact]
    public void Run_EmptyTranscriptsFolder_CouldNotRun()
    {
        Assert.Equal(Test.CouldNotRun, Test.Run(".", root.FullName, update: false, output, error, color: false));
    }

    [Fact]
    public void Run_NoStory_CouldNotRun()
    {
        Directory.Delete(Path.Combine(root.FullName, "story"), recursive: true);

        Assert.Equal(Test.CouldNotRun, Test.Run(".", root.FullName, update: false, output, error, color: false));
    }

    [Fact]
    public void Run_KitchenSample_Passes()
    {
        int exitCode = Test.Run("samples/kitchen", RepositoryRoot.Path, update: false, output, error, color: false);

        Assert.Equal(Test.Passed, exitCode);
        Assert.Matches(@"^Ran \d+ transcripts: \d+ passed, 0 failed\.\n$", output.ToString().ReplaceLineEndings("\n"));
    }

    private void WriteTranscript(string name, string text) => File.WriteAllText(Path.Combine(root.FullName, "transcripts", name), text);
}
