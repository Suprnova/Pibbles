using Pibbles.Cli.Transcripts;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Syntax;
using Pibbles.Tests.Syntax;

namespace Pibbles.Tests.Cli;

/// <summary>
/// Replays every <c>.transcript</c> file against its story and compares what it prints with the file. A transcript is a
/// script with every step written in, so it replays to itself. <c>name.variant.transcript</c> belongs to <c>name.pib</c>
/// beside it; the sample story's transcripts are in <c>samples/kitchen/transcripts</c>.
/// </summary>
public class TranscriptTests
{
    private static readonly string TestFolder = Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Transcripts");

    private static readonly string KitchenFolder = Path.Combine(RepositoryRoot.Path, "samples", "kitchen");

    public static TheoryData<string> Files { get; } = [.. FindTranscripts().Select(path => Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/'))];

    [Theory]
    [MemberData(nameof(Files))]
    public void Play_Transcript_PrintsItselfExactly(string file)
    {
        string path = Path.Combine(RepositoryRoot.Path, file);
        string expected = TranscriptFile.Normalize(File.ReadAllText(path));

        string actual = TranscriptPlayer.Play(StoryOf(path), expected);

        if (actual != expected)
            File.WriteAllText(Path.ChangeExtension(path, ".received.transcript"), actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Transcripts_CoverEveryStepKindAndMarkerKind()
    {
        HashSet<Type> seen = [];
        foreach (string path in FindTranscripts())
            TranscriptPlayer.Play(StoryOf(path), File.ReadAllText(path), seen);

        Type[] all = [.. typeof(Story).Assembly.GetTypes().Where(type => !type.IsAbstract && (type.IsSubclassOf(typeof(DialogueStep)) || type.IsSubclassOf(typeof(Marker))))];
        Assert.Empty(all.Where(type => !seen.Contains(type)).Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Transcripts_SourcesCoverEveryStatementAndInlineElement()
    {
        HashSet<Type> seen = [];
        foreach (SourceText source in FindTranscripts().SelectMany(SourcesOf).DistinctBy(source => source.Path))
        {
            foreach (SyntaxNode node in NodeFields.DescendantsAndSelf(SyntaxTree.Parse(source).Root))
                seen.Add(node.GetType());
        }

        string[] missing =
        [
            .. typeof(SyntaxTree).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && (type.IsSubclassOf(typeof(StatementSyntax)) || type.IsSubclassOf(typeof(InlineSyntax))) && !seen.Contains(type))
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Empty(missing);
    }

    [Fact]
    public void Play_StepsThatLookLikeDirectivesAndAnswers_StayText()
    {
        Story story = Compile("@actor mira\n    name: Mira\n\n== t.n\nstart the engine\nmira: > look\nmira: set $x = 1\nmira: stub it = 2\n-> > pick me #id:o1\n    @end\n");

        string transcript = TranscriptPlayer.Play(story, "start t.n\n> #o1\n");

        Assert.All(transcript.Split('\n').Skip(2).Where(line => line.Length > 0 && !line.StartsWith("> ", StringComparison.Ordinal) && !line.StartsWith("start ", StringComparison.Ordinal)), line => Assert.StartsWith("  ", line));
        Assert.Equal(transcript, TranscriptPlayer.Play(story, transcript));
        Assert.Contains("  narration: start the engine", transcript);
        Assert.Contains("  mira: > look", transcript);
    }

    [Fact]
    public void Play_TextWithLineBreaksAndBackslashes_StaysOnOneLinePerStep()
    {
        Story story = Compile("@actor mira\n    name: Mira\n\n== t.n\nmira: One{br}start two \\\\ three #id:l1\n");

        string transcript = TranscriptPlayer.Play(story, "start t.n\n");

        Assert.Contains("  mira: One\\nstart two \\\\ three  #l1", transcript);
        Assert.DoesNotContain("\nstart two", transcript);
    }

    [Fact]
    public void Play_FunctionsWithoutStubs_FailAtStartListingEachOne()
    {
        Story story = Compile("@function a() -> bool\n@function b() -> bool\n@function c() -> bool\n@actor mira\n    name: Mira\n\n== t.n\n@if a() and b() and c()\n    mira: x #id:l1\n");

        var exception = Assert.Throws<TranscriptException>(() => TranscriptPlayer.Play(story, "start t.n\nstub b = true\n"));

        Assert.Contains("`a`", exception.Message);
        Assert.Contains("`c`", exception.Message);
        Assert.DoesNotContain("`b`", exception.Message);
    }

    [Fact]
    public void Play_FunctionCalledWithArgumentsThatHaveNoStub_FailsNamingTheCall()
    {
        Story story = Compile("@function price(item: string) -> number\n@actor mira\n    name: Mira\n\n== t.n\nmira: {price(\"rope\")} #id:l1\n");

        var exception = Assert.Throws<TranscriptException>(() => TranscriptPlayer.Play(story, "start t.n\nstub price(\"key\") = 1\n"));

        Assert.Contains("price(\"rope\")", exception.Message);
    }

    [Fact]
    public void Play_ScriptThatRunsOutOfAnswers_FailsNamingTheChoice()
    {
        Story story = Compile("@actor mira\n    name: Mira\n\n== t.n\n-> First #id:o1\n-> Second #id:o2\n");

        var exception = Assert.Throws<TranscriptException>(() => TranscriptPlayer.Play(story, "start t.n\n"));

        Assert.Contains("#o1", exception.Message);
        Assert.Contains("#o2", exception.Message);
    }

    [Theory]
    [InlineData("> 9")]
    [InlineData("> #nope")]
    [InlineData("> 2")]
    public void Play_AnswerThatIsNotOnOffer_Fails(string answer)
    {
        Story story = Compile("@var $key = false\n\n== t.n\n-> First #id:o1\n-> Second  @if $key #id:o2\n");

        Assert.Throws<TranscriptException>(() => TranscriptPlayer.Play(story, $"start t.n\n{answer}\n"));
    }

    [Fact]
    public void Play_ScriptWithoutStart_Fails()
    {
        Story story = Compile("== t.n\n");

        Assert.Throws<TranscriptException>(() => TranscriptPlayer.Play(story, "set $x = 1\n"));
    }

    [Fact]
    public void Play_UnknownNode_FailsWithTheRunnersSuggestion()
    {
        Story story = Compile("@actor mira\n    name: Mira\n\n== t.next\nmira: x #id:l1\n");

        var exception = Assert.Throws<TranscriptException>(() => TranscriptPlayer.Play(story, "start t.nxt\n"));

        Assert.Contains("Did you mean `t.next`?", exception.Message);
    }

    [Fact]
    public void Parse_TranscriptAndScript_ReadTheSameDirectivesAndEquivalentAnswers()
    {
        string path = Path.Combine(TestFolder, "choices.transcript");
        string script = "start c.main\n> 2\n> 3\n> 5\n";
        string transcript = TranscriptPlayer.Play(StoryOf(path), script);

        Assert.Equal(["2", "3", "5"], TranscriptScript.Parse(script).Answers);
        Assert.Equal(["#c02", "#c03", "5"], TranscriptScript.Parse(transcript).Answers);
        Assert.Equal(TranscriptScript.Parse(script).Starts, TranscriptScript.Parse(transcript).Starts);
    }

    private static Story Compile(string text)
    {
        CompileResult result = StoryCompiler.Compile([new("story.pib", text)]);
        return result.Story ?? throw new Xunit.Sdk.XunitException(string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")));
    }

    private static Story StoryOf(string transcriptPath)
    {
        CompileResult result = StoryCompiler.Compile(SourcesOf(transcriptPath));
        return result.Story ?? throw new Xunit.Sdk.XunitException(string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Path()} {diagnostic.Code} {diagnostic.Message}")));
    }

    private static SourceText[] SourcesOf(string transcriptPath)
    {
        string[] paths = Path.GetFullPath(transcriptPath).StartsWith(KitchenFolder, StringComparison.OrdinalIgnoreCase)
            ? [.. Directory.EnumerateFiles(Path.Combine(KitchenFolder, "story"), "*.pib", SearchOption.AllDirectories)]
            : [Path.Combine(Path.GetDirectoryName(transcriptPath)!, Path.GetFileName(transcriptPath).Split('.')[0] + ".pib")];
        return [.. paths.Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/'), File.ReadAllText(path)))];
    }

    private static IEnumerable<string> FindTranscripts() =>
        new[] { TestFolder, Path.Combine(KitchenFolder, "transcripts") }
            .Where(Directory.Exists)
            .SelectMany(TranscriptFile.Find)
            .Order(StringComparer.Ordinal);
}

internal static class DiagnosticExtensions
{
    public static string Path(this Pibbles.Diagnostics.Diagnostic diagnostic) => diagnostic.Location.Path;
}
