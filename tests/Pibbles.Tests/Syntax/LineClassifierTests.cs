using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class LineClassifierTests
{
    [Theory]
    [InlineData("== kitchen.door", nameof(LineKind.Header))]
    [InlineData("@if $door_open", nameof(LineKind.At))]
    [InlineData("-> Rattle the handle", nameof(LineKind.Option))]
    [InlineData("- mira: Nope.", nameof(LineKind.Dash))]
    [InlineData("-\tmira: Nope.", nameof(LineKind.Dash))]
    [InlineData("mira: Locked.", nameof(LineKind.Text))]
    [InlineData("If you say so.", nameof(LineKind.Text))]
    [InlineData("-", nameof(LineKind.Dash))]
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
    public void Classify_ByteOrderMark_ReportsItAndClassifiesTheRest()
    {
        var source = new SourceText("story.pib", "\uFEFF== kitchen.door");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal((LineKind.Header, new TextSpan(1, 15)), (classified.Tokens[0].Line.Kind, classified.Tokens[0].Line.Content));
        Diagnostic diagnostic = Assert.Single(classified.Diagnostics);
        Assert.Equal(("PIB1007", new TextSpan(0, 1)), (diagnostic.Code, diagnostic.Location.Span));
    }

    [Fact]
    public void Classify_ByteOrderMarkAlone_ReportsItAndIsBlank()
    {
        var source = new SourceText("story.pib", "\uFEFF");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([LineKind.EndOfFile], classified.Tokens.Select(token => token.Line.Kind));
        Assert.Equal([("PIB1007", 0)], Codes(classified));
    }

    [Fact]
    public void Classify_ByteOrderMarkAfterTheFirstCharacter_IsText()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n\uFEFFmira: Hm.");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal(["== kitchen.door", "\uFEFFmira: Hm.", "EndOfFile"], Describe(source, classified));
        Assert.Empty(classified.Diagnostics);
    }

    [Fact]
    public void Classify_NestedBlocks_IndentsAndDedents()
    {
        var source = new SourceText("story.pib", "@if a\n    @if b\n        x\n    y\nz\n@if c\n    w");

        string[] tokens = Describe(source, LineClassifier.Classify(source));

        Assert.Equal(["@if a", "Indent", "@if b", "Indent", "x", "Dedent", "y", "Dedent", "z", "@if c", "Indent", "w", "Dedent", "EndOfFile"], tokens);
    }

    [Fact]
    public void Classify_CommentBetweenBlocks_DoesNotDedent()
    {
        var source = new SourceText("story.pib", "@if a\n    x\n// A comment at the top.\n\n    y");

        string[] tokens = Describe(source, LineClassifier.Classify(source));

        Assert.Equal(["@if a", "Indent", "x", "y", "Dedent", "EndOfFile"], tokens);
    }

    [Fact]
    public void Classify_LineBetweenBlockWidths_JoinsOuterBlockAndReportsIt()
    {
        var source = new SourceText("story.pib", "@if a\n        x\n    y\nz");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal(["@if a", "Indent", "x", "Dedent", "y", "z", "EndOfFile"], Describe(source, classified));
        Assert.Equal([("PIB1002", 2)], Codes(classified));
    }

    [Theory]
    [InlineData("@if a\n    x\n@if b\n\ty", "tabs", "spaces")]
    [InlineData("@if a\n\tx\n@if b\n    y", "spaces", "tabs")]
    [InlineData("@if a\n    x\n@if b\n \ty", "tabs and spaces", "spaces")]
    public void Classify_IndentWithOtherCharacter_MessageSaysWhatEachUses(string text, string line, string file)
    {
        var source = new SourceText("story.pib", text);

        Diagnostic diagnostic = Assert.Single(LineClassifier.Classify(source).Diagnostics);

        Assert.Equal(("PIB1001", $"This line indents with {line}, but the file indents with {file}, so I can't tell which block it belongs to."), (diagnostic.Code, diagnostic.Message));
    }

    [Fact]
    public void Classify_FirstIndentMixesCharacters_ReportsIt()
    {
        var source = new SourceText("story.pib", "@if a\n\t x");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([("PIB1001", 1)], Codes(classified));
    }

    [Fact]
    public void Classify_MixedLineAtNoBlockWidth_ReportsOnlyMixing()
    {
        var source = new SourceText("story.pib", "@if a\n    @if b\n        x\n\t\ty");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([("PIB1001", 3)], Codes(classified));
    }

    [Fact]
    public void Classify_TabIndentedRegionInSpacesFile_KeepsItsStructure()
    {
        var source = new SourceText("story.pib", "@if a\n    x\n@if b\n\ty\n\t@if c\n\t\tz");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal(["@if a", "Indent", "x", "Dedent", "@if b", "Indent", "y", "@if c", "Indent", "z", "Dedent", "Dedent", "EndOfFile"], Describe(source, classified));
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

    [Fact]
    public void Classify_OptionBodyIndentedWithOtherSpace_KeepsItsOptionAndReportsOnce()
    {
        var source = new SourceText("story.pib", $"-> Ask\n{Spaces.Ideographic}{Spaces.Ideographic}mira: Hm.\n-> Leave");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal(["-> Ask", "Indent", "mira: Hm.", "Dedent", "-> Leave", "EndOfFile"], Describe(source, classified));
        Assert.Equal([("PIB1006", 1)], Codes(classified));
    }

    [Theory]
    [InlineData("mira: Hi.\n{0}mira: Hm.")]
    [InlineData("== kitchen.door\n{0}mira: Hm.")]
    public void Classify_ParagraphIndent_DoesNotIndentAndReportsOnce(string template)
    {
        var source = new SourceText("story.pib", template.Replace("{0}", Spaces.Ideographic, StringComparison.Ordinal));

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.DoesNotContain("Indent", Describe(source, classified));
        Assert.Equal([("PIB1006", 1)], Codes(classified));
    }

    [Fact]
    public void Classify_TabsThenOtherSpace_ReportsItAsNonIndentingNotAsMixed()
    {
        var source = new SourceText("story.pib", $"@if a\n\t\tx\n    {Spaces.NoBreak}y");

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([("PIB1006", 2)], Codes(classified));
    }

    [Theory]
    [InlineData(Spaces.NoBreak, "U+00A0 (a no-break space)")]
    [InlineData(Spaces.NarrowNoBreak, "U+202F (a narrow no-break space)")]
    [InlineData(Spaces.Ideographic, "U+3000 (an ideographic space)")]
    [InlineData("\x2003", "U+2003")]
    public void Classify_OtherSpace_NamesTheFirstOne(string space, string expected)
    {
        var source = new SourceText("story.pib", $"-> Ask\n  {space}{Spaces.Ideographic}mira: Hm.");

        Diagnostic diagnostic = Assert.Single(LineClassifier.Classify(source).Diagnostics);

        Assert.Equal(($"This line starts with {expected}, which doesn't indent.", new TextSpan(7, 4)), (diagnostic.Message, diagnostic.Location.Span));
    }

    [Theory]
    [InlineData(Spaces.Ideographic)]
    [InlineData($"{Spaces.Ideographic}// A comment.")]
    [InlineData($"  {Spaces.NoBreak}")]
    public void Classify_BlankOrCommentWithOtherSpace_LeavesItOutAndReportsNothing(string text)
    {
        var source = new SourceText("story.pib", text);

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([LineKind.EndOfFile], classified.Tokens.Select(token => token.Line.Kind));
        Assert.Empty(classified.Diagnostics);
    }

    [Fact]
    public void Classify_OtherSpaceInsideText_IsNotIndentation()
    {
        string line = $"mira: Done!{Spaces.Ideographic}Next.{Spaces.Ideographic}";
        var source = new SourceText("story.pib", line);

        ClassifiedLines classified = LineClassifier.Classify(source);

        Assert.Equal([line, "EndOfFile"], Describe(source, classified));
        Assert.Empty(classified.Diagnostics);
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
