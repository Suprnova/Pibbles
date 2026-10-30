using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class WhitespaceTests
{
    [Theory]
    [InlineData("a  \t b", 1, 5)]
    [InlineData("a  \t b", 0, 0)]
    [InlineData("a b", 2, 2)]
    [InlineData("a -b", 1, 2)]
    [InlineData("a   ", 1, 4)]
    public void Skip_SpacesAndTabs_ReturnsFirstOtherPosition(string text, int start, int expected) =>
        Assert.Equal(expected, Whitespace.Skip(text, start, text.Length));

    [Fact]
    public void Skip_WhitespacePastEnd_StopsAtEnd()
    {
        const string text = "a     b";

        int position = Whitespace.Skip(text, 1, 3);

        Assert.Equal(3, position);
    }
}
