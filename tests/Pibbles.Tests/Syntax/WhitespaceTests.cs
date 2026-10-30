using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class WhitespaceTests
{
    [Theory]
    [InlineData(0x0020, true)]
    [InlineData(0x0009, true)]
    [InlineData(0x00A0, true)]
    [InlineData(0x1680, true)]
    [InlineData(0x2003, true)]
    [InlineData(0x202F, true)]
    [InlineData(0x205F, true)]
    [InlineData(0x3000, true)]
    [InlineData(0x000A, false)]
    [InlineData(0x000D, false)]
    [InlineData(0x0085, false)]
    [InlineData(0x200B, false)]
    [InlineData(0xFEFF, false)]
    [InlineData(0x0061, false)]
    public void Is_Character_IsTabOrSpaceSeparator(int codePoint, bool expected) =>
        Assert.Equal(expected, Whitespace.Is((char)codePoint));

    [Theory]
    [InlineData("a  \t b", 1, 5)]
    [InlineData("a  \t b", 0, 0)]
    [InlineData("a b", 2, 2)]
    [InlineData("a -b", 1, 2)]
    [InlineData("a   ", 1, 4)]
    public void Skip_SpacesAndTabs_ReturnsFirstOtherPosition(string text, int start, int expected) =>
        Assert.Equal(expected, Whitespace.Skip(text, start, text.Length));

    [Fact]
    public void Skip_OtherSpaceSeparators_SkipsThemToo()
    {
        string text = $"a {Spaces.Ideographic}{Spaces.NoBreak}b";

        int position = Whitespace.Skip(text, 1, text.Length);

        Assert.Equal(4, position);
    }

    [Fact]
    public void Skip_WhitespacePastEnd_StopsAtEnd()
    {
        const string text = "a     b";

        int position = Whitespace.Skip(text, 1, 3);

        Assert.Equal(3, position);
    }

    [Theory]
    [InlineData("  a b  ", "a b  ", "  a b", "a b")]
    [InlineData("ab", "ab", "ab", "ab")]
    [InlineData("\t ", "", "", "")]
    public void Trim_SpacesAndTabs_RemovesThemFromTheEnds(string text, string start, string end, string both)
    {
        Assert.Equal(start, Whitespace.TrimStart(text).ToString());
        Assert.Equal(end, Whitespace.TrimEnd(text).ToString());
        Assert.Equal(both, Whitespace.Trim(text).ToString());
    }

    [Fact]
    public void Trim_OtherSpaceSeparators_RemovesThemToo()
    {
        string text = $"{Spaces.Ideographic}a b{Spaces.NoBreak}{Spaces.NarrowNoBreak}";

        string trimmed = Whitespace.Trim(text).ToString();

        Assert.Equal("a b", trimmed);
    }
}
