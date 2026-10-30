using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class IdentifiersTests
{
    [Theory]
    [InlineData("a", true)]
    [InlineData("_", true)]
    [InlineData("é", true)]
    [InlineData("\U00010400", true)]
    [InlineData("1", false)]
    [InlineData("٣", false)]
    [InlineData("́", false)]
    [InlineData("-", false)]
    public void IsStart_Character_IsLetterOrUnderscore(string text, bool expected) =>
        Assert.Equal(expected, Identifiers.IsStart(text, 0));

    [Theory]
    [InlineData("a", true)]
    [InlineData("_", true)]
    [InlineData("1", true)]
    [InlineData("́", true)]
    [InlineData("\U00010400", true)]
    [InlineData("٣", false)]
    [InlineData("-", false)]
    [InlineData(".", false)]
    public void IsPart_Character_IsLetterMarkAsciiDigitOrUnderscore(string text, bool expected) =>
        Assert.Equal(expected, Identifiers.IsPart(text, 0));

    [Theory]
    [InlineData("kitchen door", 0, 7)]
    [InlineData("_a1_b.c", 0, 5)]
    [InlineData("café!", 0, 4)]
    [InlineData("café!", 0, 5)]
    [InlineData("\U00010400\U00010400x", 0, 5)]
    [InlineData("a b", 2, 3)]
    public void Skip_IdentifierAtStart_ReturnsItsEnd(string text, int start, int expected) =>
        Assert.Equal(expected, Identifiers.Skip(text, start, text.Length));

    [Theory]
    [InlineData("1a", 0)]
    [InlineData("́a", 0)]
    [InlineData(" a", 0)]
    [InlineData("a", 1)]
    public void Skip_NoIdentifierAtStart_ReturnsStart(string text, int start) =>
        Assert.Equal(start, Identifiers.Skip(text, start, text.Length));

    [Fact]
    public void Skip_IdentifierPastEnd_StopsAtEnd()
    {
        const string text = "kitchen";

        int end = Identifiers.Skip(text, 0, 3);

        Assert.Equal(3, end);
    }
}
