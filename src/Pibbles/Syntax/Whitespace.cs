using System.Globalization;

namespace Pibbles.Syntax;

/// <summary>What separates tokens within a line, read the same way by the code lexer, the speaker scanner and the parser.</summary>
/// <remarks>Whitespace is a tab or any space separator (Unicode category Zs). Indentation is narrower: only spaces and tabs indent.</remarks>
internal static class Whitespace
{
    /// <summary>Whether <paramref name="c"/> is a tab or a space separator, such as U+0020, U+00A0 or the ideographic space U+3000.</summary>
    public static bool Is(char c) => c is ' ' or '\t' || c > 127 && char.GetUnicodeCategory(c) is UnicodeCategory.SpaceSeparator;

    /// <summary>The position of the first character from <paramref name="start"/> that isn't whitespace, or <paramref name="end"/> if there is none.</summary>
    public static int Skip(string text, int start, int end)
    {
        while (start < end && Is(text[start]))
            start++;

        return start;
    }

    /// <summary><paramref name="text"/> without the whitespace at its start.</summary>
    public static ReadOnlySpan<char> TrimStart(ReadOnlySpan<char> text)
    {
        int start = 0;
        while (start < text.Length && Is(text[start]))
            start++;

        return text[start..];
    }

    /// <summary><paramref name="text"/> without the whitespace at its end.</summary>
    public static ReadOnlySpan<char> TrimEnd(ReadOnlySpan<char> text)
    {
        int end = text.Length;
        while (end > 0 && Is(text[end - 1]))
            end--;

        return text[..end];
    }

    /// <summary><paramref name="text"/> without the whitespace at either end.</summary>
    public static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text) => TrimEnd(TrimStart(text));
}
