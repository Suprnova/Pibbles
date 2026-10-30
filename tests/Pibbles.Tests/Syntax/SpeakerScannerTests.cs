using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class SpeakerScannerTests
{
    [Theory]
    [InlineData("mira: Hi.", "mira", 4)]
    [InlineData("Mira : Hi.", "Mira", 5)]
    [InlineData("mira:", "mira", 4)]
    public void Scan_NameAndColon_IsSpeakerWithNothingBetween(string text, string name, int colon)
    {
        SpeakerPrefix prefix = Scan(text)!.Value;

        Assert.Equal((true, name, colon), (prefix.IsSpeaker, Read(text, prefix.Name), prefix.Colon));
        Assert.Equal((null, null), (prefix.Parentheses, prefix.Extra));
        Assert.Empty(prefix.Values);
    }

    [Theory]
    [InlineData($"guide{Spaces.NoBreak}: Bonjour.", "guide", 6)]
    [InlineData($"guide:{Spaces.Ideographic}Bonjour.", "guide", 5)]
    [InlineData($"guide{Spaces.NarrowNoBreak}:{Spaces.NoBreak}Bonjour.", "guide", 6)]
    public void Scan_OtherSpaceSeparator_ActsLikeASpace(string text, string name, int colon)
    {
        SpeakerPrefix prefix = Scan(text)!.Value;

        Assert.Equal((true, name, colon), (prefix.IsSpeaker, Read(text, prefix.Name), prefix.Colon));
    }

    [Theory]
    [InlineData("guide：こんにちは。", "guide", 5)]
    [InlineData("guide ：", "guide", 6)]
    [InlineData("田中：こんにちは。", "田中", 2)]
    public void Scan_FullWidthColon_IsSpeakerWithNoSpaceNeeded(string text, string name, int colon)
    {
        SpeakerPrefix prefix = Scan(text)!.Value;

        Assert.Equal((true, name, colon), (prefix.IsSpeaker, Read(text, prefix.Name), prefix.Colon));
    }

    [Theory]
    [InlineData("mira (sad, happy): Hi.", "mira", "sad|happy")]
    [InlineData("mira (sad): Hi.", "mira", "sad")]
    [InlineData("(sad, happy): Hi.", null, "sad|happy")]
    [InlineData("田中（笑顔、揺れ）：こんにちは。", "田中", "笑顔|揺れ")]
    [InlineData("mira（sad，happy）: Hi.", "mira", "sad|happy")]
    [InlineData("mira (sad、happy）：Hi.", "mira", "sad|happy")]
    [InlineData("mira（sad）: Hi.", "mira", "sad")]
    [InlineData("（揺れ）：こんにちは。", null, "揺れ")]
    [InlineData("(sad, happy)：", null, "sad|happy")]
    public void Scan_ParenthesisOfNames_IsSpeakerWithValues(string text, string? name, string values)
    {
        SpeakerPrefix prefix = Scan(text)!.Value;

        Assert.Equal((true, name, values), (prefix.IsSpeaker, Read(text, prefix.Name), string.Join('|', prefix.Values!.Select(value => Read(text, value)))));
    }

    [Theory]
    [InlineData("Mira:hi")]
    [InlineData("https://x")]
    [InlineData("mira (): Hi.")]
    [InlineData("mira (a,): Hi.")]
    [InlineData("(): Hi.")]
    [InlineData("（）：Hi.")]
    [InlineData("mira（a、）：Hi.")]
    [InlineData("mira（to Rex）：Hi.")]
    [InlineData("mira（sad）:Hi.")]
    public void Scan_PrefixThatAlmostLooksLikeASpeaker_IsNotASpeaker(string text)
    {
        SpeakerPrefix? prefix = Scan(text);

        Assert.False(prefix?.IsSpeaker);
        Assert.NotNull(prefix);
    }

    [Fact]
    public void Scan_WordAfterParenthesis_IsExtraAndNotASpeaker()
    {
        const string text = "mira (sad) loudly: Hi.";

        SpeakerPrefix prefix = Scan(text)!.Value;

        Assert.Equal((false, "loudly", "mira"), (prefix.IsSpeaker, Read(text, prefix.Extra), Read(text, prefix.Name)));
    }

    [Fact]
    public void Scan_FullWidthParenthesesAfterValues_AreExtraAndNotASpeaker()
    {
        const string text = "mira（sad）（shake）：Hi.";

        SpeakerPrefix prefix = Scan(text)!.Value;

        Assert.Equal((false, "（shake）"), (prefix.IsSpeaker, Read(text, prefix.Extra)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("mira")]
    [InlineData("Hello there.")]
    [InlineData("mira (sad: Hi.")]
    [InlineData("(sad: Hi.")]
    [InlineData("(sad) Hi.")]
    [InlineData("(sad)hi: Hi.")]
    [InlineData("(sad) loudly: Hi.")]
    [InlineData("mira（sad：Hi.")]
    [InlineData("（sad：Hi.")]
    [InlineData("他说\\：“你好。”")]
    public void Scan_NoColonOrUnclosedParenthesis_IsNull(string text) => Assert.Null(Scan(text));

    private static SpeakerPrefix? Scan(string text) => SpeakerScanner.Scan(text, 0, text.Length);

    private static string? Read(string text, TextSpan? span) => span is { } found ? text.Substring(found.Start, found.Length) : null;
}
