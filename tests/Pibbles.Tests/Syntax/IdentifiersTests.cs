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
    [InlineData("１", false)]
    [InlineData("＿", false)]
    [InlineData("٣", false)]
    [InlineData("́", false)]
    [InlineData("-", false)]
    public void IsStart_Character_IsLetterOrUnderscore(string text, bool expected) =>
        Assert.Equal(expected, Identifiers.IsStart(text, 0));

    [Theory]
    [InlineData("a", true)]
    [InlineData("_", true)]
    [InlineData("1", true)]
    [InlineData("１", true)]
    [InlineData("＿", true)]
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

    [Theory]
    [InlineData("kitchen")]
    [InlineData("café")]
    [InlineData("田中")]
    [InlineData("한국")]
    public void Composed_ComposedName_IsNull(string name) =>
        Assert.Null(Identifiers.Composed(name, new(0, name.Length)));

    [Theory]
    [InlineData("café", "café")]
    [InlineData("zoë", "zoë")]
    [InlineData("한", "한")]
    public void Composed_DecomposedName_IsComposed(string name, string expected) =>
        Assert.Equal(expected, Identifiers.Composed(name, new(0, name.Length)));

    [Fact]
    public void Composed_SpanInsideText_ComposesOnlyTheSpan()
    {
        const string text = "é café é";

        string? composed = Identifiers.Composed(text, new(3, 5));

        Assert.Equal("café", composed);
    }

    [Theory]
    [InlineData("guide", "Ordinary")]
    [InlineData("田中", "Ordinary")]
    [InlineData("caf\u00E9", "Ordinary")]
    [InlineData("ｇｕｉｄｅ", "FullWidth")]
    [InlineData("score１", "FullWidth")]
    [InlineData("a＿b", "FullWidth")]
    [InlineData("ｶﾞｲﾄﾞ", "HalfWidthKatakana")]
    [InlineData("ﾡﾢ", "HalfWidthHangul")]
    [InlineData("a\uFFA0b", "HangulFiller")]
    [InlineData("cafe\u0301", "Decomposed")]
    [InlineData("ｃａｆe\u0301", "FullWidth")]
    [InlineData("ｇｕｉｄｅｶ", "HalfWidthKatakana")]
    public void FormOf_Identifier_IsHowItIsWritten(string name, string expected) =>
        Assert.Equal(expected, Identifiers.FormOf(name, new(0, name.Length)).ToString());

    [Fact]
    public void Skip_IdentifierPastEnd_StopsAtEnd()
    {
        const string text = "kitchen";

        int end = Identifiers.Skip(text, 0, 3);

        Assert.Equal(3, end);
    }
}
