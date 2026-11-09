using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class MarkerFormsTests
{
    [Theory]
    [InlineData('＝', '=')]
    [InlineData('＠', '@')]
    [InlineData('（', '(')]
    [InlineData('：', ':')]
    [InlineData('０', '0')]
    [InlineData('９', '9')]
    [InlineData('「', '[')]
    [InlineData('【', '[')]
    [InlineData('」', ']')]
    [InlineData('】', ']')]
    [InlineData('『', '{')]
    [InlineData('』', '}')]
    [InlineData('・', '/')]
    [InlineData('、', ',')]
    [InlineData('。', '.')]
    [InlineData('￥', '$')]
    [InlineData('《', '<')]
    [InlineData('》', '>')]
    [InlineData('“', '"')]
    [InlineData('”', '"')]
    public void Map_Form_IsTheMarkerItStandsFor(char form, char marker) => Assert.Equal(marker, MarkerForms.Map(form));

    [Theory]
    [InlineData('ａ')]
    [InlineData('Ｚ')]
    [InlineData('＿')]
    [InlineData('ー')]
    [InlineData('あ')]
    [InlineData('=')]
    [InlineData('　')]
    public void Map_LetterOrOrdinaryCharacter_IsNoMarker(char c) => Assert.Equal('\0', MarkerForms.Map(c));

    [Fact]
    public void Map_TextWithoutForms_IsTheSameString()
    {
        string text = "@set $a = 1 // アイ";

        string mapped = MarkerForms.Map(text);

        Assert.Same(text, mapped);
    }

    [Fact]
    public void Map_TextWithFormsAndFullWidthLetters_MapsAndNarrowsInPlace() => Assert.Equal("@set $a_1 = [AI]", MarkerForms.Map("@set ＄ａ＿１ ＝ 「ＡＩ」"));

    [Theory]
    [InlineData('ｗ', 'w')]
    [InlineData('Ｚ', 'Z')]
    [InlineData('５', '5')]
    [InlineData('＿', '_')]
    [InlineData('ｶ', 'ｶ')]
    [InlineData('あ', 'あ')]
    [InlineData('＝', '＝')]
    public void Narrow_Character_IsItsAsciiFormIfItHasOne(char c, char expected) => Assert.Equal(expected, MarkerForms.Narrow(c));

    [Fact]
    public void Report_FormRecordedTwice_IsReportedOnce()
    {
        var source = new SourceText("story.pib", "@set $a ＝ 1");
        var forms = new MarkerFormLog();
        forms.Record(8, '=');

        forms.Record(8, '=');

        Assert.Single(forms.Report(source));
    }

    [Fact]
    public void Report_AdjacentForms_AreOneDiagnosticNamingEachCodePointOnce()
    {
        var source = new SourceText("story.pib", "@if a ＝＝（b）");
        var forms = new MarkerFormLog();
        forms.Record(6, '=');
        forms.Record(7, '=');
        forms.Record(8, '(');
        forms.Record(10, ')');

        Diagnostic[] diagnostics = [.. forms.Report(source)];

        Assert.Equal(
            ["I read `＝＝（` (U+FF1D, U+FF08) as `==(`, but markers are typed half-width.", "I read `）` (U+FF09) as `)`, but markers are typed half-width."],
            diagnostics.Select(diagnostic => diagnostic.Message));
        Assert.All(diagnostics, diagnostic => Assert.Equal("Write `@if a ==(b)`, typing markers in your input method's direct (half-width) mode.", diagnostic.Help));
    }
}
