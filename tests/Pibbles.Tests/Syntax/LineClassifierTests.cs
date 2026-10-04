using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class LineClassifierTests
{
    [Theory]
    [InlineData("== kitchen.door", nameof(LineKind.Header))]
    [InlineData("@if $door_open:", nameof(LineKind.At))]
    [InlineData("-> Rattle the handle", nameof(LineKind.Option))]
    [InlineData("- mira: Nope.", nameof(LineKind.Dash))]
    [InlineData("-\tmira: Nope.", nameof(LineKind.Dash))]
    [InlineData("mira: Locked.", nameof(LineKind.Text))]
    [InlineData("If you say so.", nameof(LineKind.Text))]
    [InlineData("-", nameof(LineKind.Text))]
    [InlineData("-- Dashes are text.", nameof(LineKind.Text))]
    [InlineData("= One equals sign is text.", nameof(LineKind.Text))]
    [InlineData("/ One slash is text.", nameof(LineKind.Text))]
    public void Classify_ByLeadingMarker_GivesKind(string text, string expected)
    {
        var source = new SourceText("story.pib", text);

        LineToken first = LineClassifier.Classify(source).Tokens[0];

        Assert.Equal((LineTokenKind.Line, Enum.Parse<LineKind>(expected)), (first.Kind, first.Line.Kind));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("// A comment.")]
    [InlineData("    // An indented comment.")]
    [InlineData("//// A banner.")]
    [InlineData("/// A note.")]
    public void Classify_BlankCommentOrNote_LeavesItOutOfTokens(string text)
    {
        var source = new SourceText("story.pib", text);

        IReadOnlyList<LineToken> tokens = LineClassifier.Classify(source).Tokens;

        Assert.Equal([LineKind.EndOfFile], tokens.Select(token => token.Line.Kind));
    }

    [Fact]
    public void Classify_ByteOrderMark_IsSkipped()
    {
        var source = new SourceText("story.pib", "\uFEFF== kitchen.door");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal((LineKind.Header, new TextSpan(1, 15)), (classified.Tokens[0].Line.Kind, classified.Tokens[0].Line.Content));
    }

    [Fact]
    public void Classify_NestedBlocks_IndentsAndDedents()
    {
        var source = new SourceText("story.pib", "@if a:\n    @if b:\n        x\n    y\nz\n@if c:\n    w");

        string[] tokens = Describe(source, LineClassifier.Classify(source));

        Assert.Equal(["@if a:", "Indent", "@if b:", "Indent", "x", "Dedent", "y", "Dedent", "z", "@if c:", "Indent", "w", "Dedent", "EndOfFile"], tokens);
    }

    [Fact]
    public void Classify_CommentBetweenBlocks_DoesNotDedent()
    {
        var source = new SourceText("story.pib", "@if a:\n    x\n// A comment at the top.\n\n    y");

        string[] tokens = Describe(source, LineClassifier.Classify(source));

        Assert.Equal(["@if a:", "Indent", "x", "y", "Dedent", "EndOfFile"], tokens);
    }

    [Fact]
    public void Classify_LineBetweenBlockWidths_JoinsOuterBlockAndReportsIt()
    {
        var source = new SourceText("story.pib", "@if a:\n        x\n    y\nz");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal(["@if a:", "Indent", "x", "Dedent", "y", "z", "EndOfFile"], Describe(source, classified));
        Assert.Equal([("PIB1002", 2)], Codes(classified));
    }

    [Theory]
    [InlineData("@if a:\n    x\n@if b:\n\ty", "tabs", "spaces")]
    [InlineData("@if a:\n\tx\n@if b:\n    y", "spaces", "tabs")]
    [InlineData("@if a:\n    x\n@if b:\n \ty", "tabs and spaces", "spaces")]
    public void Classify_IndentWithOtherCharacter_ReportsWhatEachUses(string text, string line, string file)
    {
        var source = new SourceText("story.pib", text);

        Diagnostic diagnostic = Assert.Single(LineClassifier.Classify(source).Diagnostics);

        Assert.Equal(("PIB1001", $"this line indents with {line}, but the file indents with {file}"), (diagnostic.Code, diagnostic.Label));
    }

    [Fact]
    public void Classify_FirstIndentMixesCharacters_ReportsIt()
    {
        var source = new SourceText("story.pib", "@if a:\n\t x");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([("PIB1001", 1)], Codes(classified));
    }

    [Fact]
    public void Classify_MixedLineAtNoBlockWidth_ReportsOnlyMixing()
    {
        var source = new SourceText("story.pib", "@if a:\n    @if b:\n        x\n\t\ty");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([("PIB1001", 3)], Codes(classified));
    }

    [Fact]
    public void Classify_TabIndentedRegionInSpacesFile_KeepsItsStructure()
    {
        var source = new SourceText("story.pib", "@if a:\n    x\n@if b:\n\ty\n\t@if c:\n\t\tz");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal(["@if a:", "Indent", "x", "Dedent", "@if b:", "Indent", "y", "@if c:", "Indent", "z", "Dedent", "Dedent", "EndOfFile"], Describe(source, classified));
        Assert.Equal([("PIB1001", 3), ("PIB1001", 4), ("PIB1001", 5)], Codes(classified));
    }

    [Theory]
    [InlineData("/// A note.", 0)]
    [InlineData("    ///", 4)]
    [InlineData("///No space.", 0)]
    public void Classify_Note_ReportsItsSlashes(string text, int start)
    {
        var source = new SourceText("story.pib", text);

        Diagnostic diagnostic = Assert.Single(LineClassifier.Classify(source).Diagnostics);

        Assert.Equal(("PIB1005", new TextSpan(start, 3)), (diagnostic.Code, diagnostic.Location.Span));
    }

    [Fact]
    public void Classify_IndentedNote_DoesNotIndent()
    {
        var source = new SourceText("story.pib", "x\n    /// A note.\ny");

        string[] tokens = Describe(source, LineClassifier.Classify(source));

        Assert.Equal(["x", "y", "EndOfFile"], tokens);
    }

    [Theory]
    [InlineData("a\nb\n")]
    [InlineData("a\r\nb\r\n")]
    [InlineData("a\rb\r")]
    public void Classify_EndOfFile_IsAtEndOfText(string text)
    {
        var source = new SourceText("story.pib", text);

        SourceLine end = LineClassifier.Classify(source).Tokens[^1].Line;

        Assert.Equal((LineKind.EndOfFile, 2, new TextSpan(text.Length, 0)), (end.Kind, end.Number, end.Content));
    }

    private static string[] Describe(SourceText source, ClassifiedLines classified) =>
        [.. classified.Tokens.Select(token => token switch
        {
            { Kind: LineTokenKind.Line, Line.Kind: LineKind.EndOfFile } => "EndOfFile",
            { Kind: LineTokenKind.Line, Line.Content: var content } => source.Text.Substring(content.Start, content.Length),
            _ => token.Kind.ToString(),
        })];

    private static (string Code, int Line)[] Codes(ClassifiedLines classified) =>
        [.. classified.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Location.Start.Line))];
}
