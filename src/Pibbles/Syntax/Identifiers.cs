using System.Globalization;
using System.Text;

namespace Pibbles.Syntax;

/// <summary>What an identifier is, read the same way by the code lexer, the speaker scanner and the parser.</summary>
internal static class Identifiers
{
    /// <summary>Whether the character at <paramref name="index"/> can start an identifier: a letter or <c>_</c>.</summary>
    public static bool IsStart(string text, int index) => char.IsLetter(text, index) || text[index] is '_';

    /// <summary>Whether the character at <paramref name="index"/> can continue an identifier: a letter, a combining mark, an ASCII digit or <c>_</c>.</summary>
    public static bool IsPart(string text, int index) =>
        char.IsLetter(text, index)
        || char.IsAsciiDigit(text[index])
        || text[index] is '_'
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
}
