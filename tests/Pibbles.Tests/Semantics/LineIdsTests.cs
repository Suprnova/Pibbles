using System.Text.RegularExpressions;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public partial class LineIdsTests
{
    private const string Story = """
        @actor mira
            name: Mira
            poses: sad
        @tag thought

        == a.b
        mira: Hello. #thought
        mira (sad):
        -> Go
            mira: Inside.
        @call a.c // Back again.
        @sequence
            - One.

        == a.c
        Already has one. #id:k7qp2x
        """;

    private readonly Random random = new(1618);

    [Fact]
    public void AddMissing_Story_AddsIdAtEndOfEachLineThatNeedsOne()
    {
        string expected = """
            @actor mira
                name: Mira
                poses: sad
            @tag thought

            == a.b
            mira: Hello. #thought #id:ID
            mira (sad):
            -> Go #id:ID
                mira: Inside. #id:ID
            @call a.c #id:ID // Back again.
            @sequence #id:ID
                - One. #id:ID

            == a.c
            Already has one. #id:k7qp2x
            """;

        string result = AddIds(Story);

        Assert.Equal(expected, NewId().Replace(result, "#id:ID"));
    }

    [Fact]
    public void AddMissing_ResultCompiledAgain_HasNoMissingIdsAndNothingToAdd()
    {
        string result = AddIds(Story);
        var compilation = Compilation.Create([new SourceText("story.pib", result)]);

        Assert.Empty(compilation.Diagnostics);
        Assert.Empty(LineIds.AddMissing(compilation, random));
    }

    [Fact]
    public void AddMissing_WindowsLineEndings_KeepsThem()
    {
        string story = Story.ReplaceLineEndings("\r\n");

        string result = AddIds(story);

        Assert.Equal(story.Split("\r\n").Length, result.Split("\r\n").Length);
        Assert.DoesNotMatch("[^\r]\n", result);
    }

    [Fact]
    public void AddMissing_IdAlreadyTaken_MakesAnother()
    {
        var story = Compilation.Create([new SourceText("story.pib", "== aaaaaa\nNarration.\n")]);
        var sequence = new SequenceRandom([0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1]);

        TextInsertion insertion = Assert.Single(LineIds.AddMissing(story, sequence));

        Assert.Equal(" #id:bbbbbb", insertion.Text);
    }

    [Fact]
    public void AddMissing_FileWithSyntaxError_LeavesItOut()
    {
        var story = Compilation.Create([new SourceText("broken.pib", "== a.b\nOne.\n@jump\n"), new SourceText("clean.pib", "== a.c\nTwo.\n")]);

        TextInsertion insertion = Assert.Single(LineIds.AddMissing(story, random));

        Assert.Equal("clean.pib", insertion.Path);
    }

    [Fact]
    public void Compile_LineWithSyntaxError_IsNotReportedForMissingId() =>
        Assert.Equal(["PIB1015"], Compilation.Create([new SourceText("story.pib", "== a.b\nI'm #winning today.\n")]).Diagnostics.Select(diagnostic => diagnostic.Code));

    private string AddIds(string text)
    {
        var compilation = Compilation.Create([new SourceText("story.pib", text)]);
        return TextInsertion.Apply(text, LineIds.AddMissing(compilation, random));
    }

    /// <summary>A generated line ID: every ID but the one the story already had.</summary>
    [GeneratedRegex("#id:(?!k7qp2x)[a-z][a-z0-9]{5}")]
    private static partial Regex NewId();

    /// <summary>A <see cref="Random"/> that gives a fixed sequence of numbers.</summary>
    private sealed class SequenceRandom(int[] values) : Random
    {
        private int next;

        public override int Next(int maxValue) => values[next++];
    }
}
