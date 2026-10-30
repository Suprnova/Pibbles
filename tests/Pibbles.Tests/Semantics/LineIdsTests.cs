using System.Text.RegularExpressions;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public partial class LineIdsTests
{
    private const string Story = """
        @channel pose
        @actor mira
            name: Mira
            pose: sad
        @tag thought

        == a.b
        mira: Hello. #thought // Aside.
        mira (sad): // Still sad.
        -> Go // Pick me.
            mira: Inside.
        -> Leave @once // Or not.
        @call a.c // Back again.
        @sequence
            - One. // First.

        == a.c
        Already has one. #id:k7qp2x
        """;

    private readonly Random random = new(1618);

    [Fact]
    public void Assign_Story_AddsIdAtEndOfEachLineThatNeedsOne()
    {
        string expected = """
            @channel pose
            @actor mira
                name: Mira
                pose: sad
            @tag thought

            == a.b
            mira: Hello. #thought #id:ID // Aside.
            mira (sad): // Still sad.
            -> Go #id:ID // Pick me.
                mira: Inside. #id:ID
            -> Leave @once #id:ID // Or not.
            @call a.c #id:ID // Back again.
            @sequence #id:ID
                - One. #id:ID // First.

            == a.c
            Already has one. #id:k7qp2x
            """;

        string result = AddIds(Story);

        Assert.Equal(expected, NewId().Replace(result, "#id:ID"));
    }

    [Fact]
    public void Assign_ResultCompiledAgain_HasNoMissingIdsAndNothingToAdd()
    {
        string result = AddIds(Story);
        var compilation = Compilation.Create([new SourceText("story.pib", result)]);

        Assert.Empty(compilation.Diagnostics);
        Assert.Empty(LineIds.Assign(compilation, random));
    }

    [Fact]
    public void Assign_WindowsLineEndings_KeepsThem()
    {
        string story = Story.ReplaceLineEndings("\r\n");

        string result = AddIds(story);

        Assert.Equal(story.Split("\r\n").Length, result.Split("\r\n").Length);
        Assert.DoesNotMatch("[^\r]\n", result);
    }

    [Fact]
    public void Assign_IdAlreadyTaken_MakesAnother()
    {
        var story = Compilation.Create([new SourceText("story.pib", "== aaaaaa\nNarration.\n")]);
        var sequence = new SequenceRandom([0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1]);

        TextEdit edit = Assert.Single(LineIds.Assign(story, sequence));

        Assert.Equal(" #id:bbbbbb", edit.Text);
    }

    [Fact]
    public void Assign_FileWithSyntaxError_LeavesItOut()
    {
        var story = Compilation.Create([new SourceText("broken.pib", "== a.b\nOne.\n@jump\n"), new SourceText("clean.pib", "== a.c\nTwo.\n")]);

        TextEdit edit = Assert.Single(LineIds.Assign(story, random));

        Assert.Equal("clean.pib", edit.Path);
    }

    [Fact]
    public void Assign_InvalidId_ReplacesTheTagAndKeepsTheRestOfTheLine()
    {
        string result = AddIds("== a.b\n@call a.c #thought #id:K7 // Aside.\n");

        Assert.Equal("== a.b\n@call a.c #thought #id:ID // Aside.\n",NewId().Replace(result, "#id:ID"));
    }

    [Theory]
    [InlineData("Hello.// Aside.", "Hello. #id:ID // Aside.")]
    [InlineData("mira: Hello.// Aside.", "mira: Hello. #id:ID // Aside.")]
    [InlineData("-> Go// Aside.\n    mira: Inside.", "-> Go #id:ID // Aside.\n    mira: Inside. #id:ID")]
    [InlineData("@call a.c// Aside.", "@call a.c #id:ID // Aside.")]
    [InlineData("@sequence// Aside.\n    - One.", "@sequence #id:ID // Aside.\n    - One. #id:ID")]
    [InlineData("@cycle// Aside.\n    - One.", "@cycle #id:ID // Aside.\n    - One. #id:ID")]
    [InlineData("@once// Aside.\n    One.", "@once #id:ID // Aside.\n    One. #id:ID")]
    [InlineData("One. #id// Aside.", "One. #id:ID // Aside.")]
    public void Assign_CommentTouchingTheLine_SpacesTheIdFromItAndCompilesCleanly(string line, string expected)
    {
        const string Header = "@actor mira\n    name: Mira\n\n== a.b\n";
        const string Footer = "\n\n== a.c\nLast. #id:k7qp2x\n";

        string result = AddIds(Header + line + Footer);
        var compilation = Compilation.Create([new SourceText("story.pib", result)]);

        Assert.Equal(Header + expected + Footer, NewId().Replace(result, "#id:ID"));
        Assert.Empty(compilation.Diagnostics);
        Assert.Empty(LineIds.Assign(compilation, random));
    }

    [Fact]
    public void Assign_InvalidIdAndMissingId_FixesBothAndCompilesCleanly()
    {
        string result = AddIds("== a.b\nOne. #id:K7\nTwo.\n");
        var compilation = Compilation.Create([new SourceText("story.pib", result)]);

        Assert.Empty(compilation.Diagnostics);
        Assert.Empty(LineIds.Assign(compilation, random));
    }

    [Fact]
    public void Assign_InvalidIdInFileWithAnotherSyntaxError_LeavesItOut()
    {
        var story = Compilation.Create([new SourceText("broken.pib", "== a.b\nOne. #id:K7\n@jump\n")]);

        Assert.Empty(LineIds.Assign(story, random));
    }

    [Fact]
    public void Apply_ReplacementAndInsertion_UsePositionsInTheOriginalText()
    {
        string result = TextEdit.Apply("abc def", [new("f.pib", 4, 3, "X"), new("f.pib", 2, 0, "-"), new("f.pib", 0, 1, "YY")]);

        Assert.Equal("YYb-c X", result);
    }

    [Fact]
    public void Compile_LineWithSyntaxError_IsNotReportedForMissingId() =>
        Assert.Equal(["PIB1015"], Compilation.Create([new SourceText("story.pib", "== a.b\nI'm #winning today.\n")]).Diagnostics.Select(diagnostic => diagnostic.Code));

    private string AddIds(string text)
    {
        var compilation = Compilation.Create([new SourceText("story.pib", text)]);
        return TextEdit.Apply(text, LineIds.Assign(compilation, random));
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
