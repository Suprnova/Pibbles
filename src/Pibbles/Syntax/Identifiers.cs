using System.Globalization;

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
}
