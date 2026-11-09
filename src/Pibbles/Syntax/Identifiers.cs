using System.Globalization;
using System.Text;

namespace Pibbles.Syntax;

/// <summary>How an identifier is written, when it isn't in its ordinary form, which is the only form a name may take.</summary>
internal enum NameForm
{
    /// <summary>In Unicode's composed form, with no half-width or full-width form.</summary>
    Ordinary,

    /// <summary>With full-width letters, digits or <c>＿</c>, which read as their ASCII forms.</summary>
    FullWidth,

    /// <summary>With half-width katakana, which have no fixed full-width form to read them as.</summary>
    HalfWidthKatakana,

    /// <summary>With half-width Hangul, which have no fixed ordinary form to read them as.</summary>
    HalfWidthHangul,

    /// <summary>With U+FFA0, the half-width Hangul filler, an invisible letter.</summary>
    HangulFiller,

    /// <summary>With an accent typed as a separate mark, so not in Unicode's composed form (NFC).</summary>
    Decomposed,
}

/// <summary>What an identifier is, read the same way by the code lexer, the speaker scanner and the parser.</summary>
internal static class Identifiers
{
    /// <summary>Whether the character at <paramref name="index"/> can start an identifier: a letter or <c>_</c>.</summary>
    public static bool IsStart(string text, int index) => char.IsLetter(text, index) || text[index] is '_';

    /// <summary>
    /// Whether the character at <paramref name="index"/> can continue an identifier: a letter, a combining mark, an ASCII
    /// digit or <c>_</c>, or a full-width digit or <c>＿</c>, which <see cref="FormOf"/> reports.
    /// </summary>
    public static bool IsPart(string text, int index) =>
        char.IsLetter(text, index)
        || text[index] is (>= '0' and <= '9') or '_' or (>= '０' and <= '９') or '＿'
        || CharUnicodeInfo.GetUnicodeCategory(text, index) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>The position just after the identifier that starts at <paramref name="start"/>, or <paramref name="start"/> if none does. Reads no further than <paramref name="end"/>.</summary>
    public static int Skip(string text, int start, int end)
    {
        if (start >= end || !IsStart(text, start))
            return start;

        int position = start;
        while (position < end && IsPart(text, position))
            position += char.IsSurrogatePair(text, position) ? 2 : 1;

        return position;
    }

    /// <summary>
    /// The text at <paramref name="span"/> in Unicode's composed form (NFC), or <see langword="null"/> if it's in that form
    /// already. A name has to be: <c>é</c> written as <c>e</c> and a separate accent looks the same, but is another name.
    /// </summary>
    /// <remarks>
    /// In .NET's invariant-globalization mode, every string counts as composed, so nothing is reported there. A story
    /// is checked by the CLI before a game in that mode compiles it.
    /// </remarks>
    public static string? Composed(string text, TextSpan span)
    {
        ReadOnlySpan<char> written = text.AsSpan(span.Start, span.Length);
        if (Ascii.IsValid(written))
            return null;

        string name = written.ToString();
        return name.IsNormalized() ? null : name.Normalize();
    }

    /// <summary>
    /// How the identifier at <paramref name="span"/> is written. A half-width form, which has no fixed ordinary form, comes
    /// first, then a full-width one, then an accent typed separately, so an identifier is reported once.
    /// </summary>
    /// <remarks>An accent typed separately is never found in .NET's invariant-globalization mode, as <see cref="Composed"/> says.</remarks>
    public static NameForm FormOf(string text, TextSpan span)
    {
        ReadOnlySpan<char> written = text.AsSpan(span.Start, span.Length);
        if (Ascii.IsValid(written))
            return NameForm.Ordinary;

        bool fullWidth = false;
        foreach (char c in written)
        {
            switch (c)
            {
                case >= 'ｦ' and <= 'ﾟ':
                    return NameForm.HalfWidthKatakana;
                case '\uFFA0':
                    return NameForm.HangulFiller;
                case >= 'ﾡ' and <= 'ￜ':
                    return NameForm.HalfWidthHangul;
                case >= '！' and <= '～':
                    fullWidth = true;
                    break;
            }
        }

        return fullWidth ? NameForm.FullWidth : written.ToString().IsNormalized() ? NameForm.Ordinary : NameForm.Decomposed;
    }
}
