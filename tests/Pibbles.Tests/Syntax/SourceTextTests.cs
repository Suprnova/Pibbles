using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class SourceTextTests
{
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\rb")]
    public void GetLinePosition_AfterEachLineBreakStyle_StartsNextLine(string text)
    {
        var source = new SourceText("story.pib", text);

        LinePosition position = source.GetLinePosition(text.Length - 1);

        Assert.Equal(new LinePosition(1, 0), position);
    }

    [Fact]
    public void GetLinePosition_AfterTab_CountsTabAsOneColumn()
    {
        var source = new SourceText("story.pib", "a\n\tmira: Hi.");

        LinePosition position = source.GetLinePosition(3);

        Assert.Equal(new LinePosition(1, 1), position);
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("mira: Hi.", 0, 9)]
    [InlineData("mira: Hi.\n", 1, 0)]
    public void GetLinePosition_AtEndOfText_IsOnLastLine(string text, int line, int column)
    {
        var source = new SourceText("story.pib", text);

        LinePosition position = source.GetLinePosition(text.Length);

        Assert.Equal(new LinePosition(line, column), position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void GetLinePosition_OutsideText_Throws(int position)
    {
        var source = new SourceText("story.pib", "abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetLinePosition(position));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("a", 1)]
    [InlineData("a\r\nb", 2)]
    [InlineData("a\n\nb\n", 4)]
    public void LineCount_CountsLastLineEvenWhenEmpty(string text, int expected)
    {
        var source = new SourceText("story.pib", text);

        Assert.Equal(expected, source.LineCount);
    }

    [Theory]
    [InlineData(0, 0, 2)]
    [InlineData(1, 4, 3)]
    [InlineData(2, 8, 0)]
    public void GetLineSpan_ExcludesLineBreak(int line, int start, int length)
    {
        var source = new SourceText("story.pib", "ab\r\ncde\n");

        TextSpan span = source.GetLineSpan(line);

        Assert.Equal(new TextSpan(start, length), span);
    }

    [Fact]
    public void GetLocation_SpanAcrossLines_HasStartAndEndPositions()
    {
        var source = new SourceText("rooms/kitchen.pib", "== kitchen.door\nmira: Locked.");
        var span = new TextSpan(3, 19);

        var location = source.GetLocation(span);

        Assert.Equal(new("rooms/kitchen.pib", span, new(0, 3), new(1, 6)), location);
    }
}
