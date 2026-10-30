namespace Pibbles.Syntax;

/// <summary>What separates tokens within a line, read the same way by the code lexer, the speaker scanner and the parser.</summary>
internal static class Whitespace
{
    /// <summary>The position of the first character from <paramref name="start"/> that isn't a space or a tab, or <paramref name="end"/> if there is none.</summary>
    public static int Skip(string text, int start, int end)
    {
        while (start < end && text[start] is ' ' or '\t')
            start++;

        return start;
    }
}
