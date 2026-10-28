namespace Pibbles.Syntax;

/// <summary>
/// What starts a text line when it looks like a speaker: a name, an optional parenthesis, and a colon, with any spacing
/// between them. Narration's own values have a parenthesis and a colon with no name before them. It's a speaker only
/// when the parenthesis holds a list of names and the colon has whitespace or the end of the line after it. Anything
/// else is narration, which the analyzer checks for lines that almost look like a speaker.
/// </summary>
/// <param name="Name">The name before the parenthesis or colon, or <see langword="null"/> when the line starts with the parenthesis.</param>
/// <param name="Parentheses">The parenthesis, brackets included, or <see langword="null"/> if there's none.</param>
/// <param name="Values">The names inside the parenthesis, separated by commas. Empty if there's no parenthesis or it holds something else; <paramref name="Parentheses"/> tells which.</param>
/// <param name="Extra">What stands between the parenthesis and the colon, or <see langword="null"/> if there's nothing: further parentheses and at most one word.</param>
/// <param name="Colon">Where the colon is.</param>
/// <param name="IsSpeaker">Whether the line has a speaker, rather than starting with something that looks like one.</param>
internal readonly record struct SpeakerPrefix(TextSpan? Name, TextSpan? Parentheses, IReadOnlyList<TextSpan> Values, TextSpan? Extra, int Colon, bool IsSpeaker);

/// <summary>Finds a <see cref="SpeakerPrefix"/> at the start of a text line.</summary>
internal static class SpeakerScanner
{
    /// <summary>Reads what starts the text between <paramref name="start"/> and <paramref name="end"/>, or returns <see langword="null"/> if it isn't a name, then an optional parenthesis, then a colon.</summary>
    public static SpeakerPrefix? Scan(string text, int start, int end)
    {
        int nameEnd = SkipIdentifier(text, start, end);
        if (nameEnd == start)
            return start < end && text[start] is '(' ? ScanNarration(text, start, end) : null;

        int position = SkipWhitespace(text, nameEnd, end);
        TextSpan? parentheses = null;
        TextSpan[] values = [];
        TextSpan? extra = null;
        if (position < end && text[position] is '(')
        {
            int close = text.IndexOf(')', position, end - position);
            if (close < 0)
                return null;

            parentheses = new TextSpan(position, close + 1 - position);
            values = ReadValues(text, position + 1, close);

            int extraStart = SkipWhitespace(text, close + 1, end);
            int extraEnd = SkipExtra(text, extraStart, end);
            if (extraEnd > extraStart)
                extra = new TextSpan(extraStart, extraEnd - extraStart);

            position = SkipWhitespace(text, extraEnd, end);
        }

        if (position >= end || text[position] is not ':')
            return null;

        bool spaced = position + 1 >= end || text[position + 1] is ' ' or '\t';
        return new(new(start, nameEnd - start), parentheses, values, extra, position, spaced && extra is null && (parentheses is null || values.Length > 0));
    }

    /// <summary>Reads narration's values: a parenthesis at the start of the line, then a colon.</summary>
    private static SpeakerPrefix? ScanNarration(string text, int start, int end)
    {
        int close = text.IndexOf(')', start, end - start);
        if (close < 0)
            return null;

        int colon = SkipWhitespace(text, close + 1, end);
        if (colon >= end || text[colon] is not ':')
            return null;

        TextSpan[] values = ReadValues(text, start + 1, close);
        bool spaced = colon + 1 >= end || text[colon + 1] is ' ' or '\t';
        return new(null, new TextSpan(start, close + 1 - start), values, null, colon, spaced && values.Length > 0);
    }

    /// <summary>Reads the names between <paramref name="start"/> and <paramref name="end"/>, separated by commas, or returns an empty list if there's anything else, or no name at all.</summary>
    private static TextSpan[] ReadValues(string text, int start, int end)
    {
        List<TextSpan> values = [];
        int position = SkipWhitespace(text, start, end);
        while (true)
        {
            int valueEnd = SkipIdentifier(text, position, end);
            if (valueEnd == position)
                return [];

            values.Add(new(position, valueEnd - position));
            position = SkipWhitespace(text, valueEnd, end);
            if (position == end)
                return [.. values];

            if (text[position] is not ',')
                return [];

            position = SkipWhitespace(text, position + 1, end);
        }
    }

    /// <summary>Skips further parentheses and at most one word, and returns where the last of them ends, or <paramref name="start"/> if there are none.</summary>
    private static int SkipExtra(string text, int start, int end)
    {
        int extraEnd = start;
        int position = start;
        bool hasWord = false;
        while (position < end)
        {
            if (text[position] is '(')
            {
                int close = text.IndexOf(')', position, end - position);
                if (close < 0)
                    break;

                extraEnd = close + 1;
            }
            else if (!hasWord && SkipIdentifier(text, position, end) is var wordEnd && wordEnd > position)
            {
                hasWord = true;
                extraEnd = wordEnd;
            }
            else
            {
                break;
            }

            position = SkipWhitespace(text, extraEnd, end);
        }

        return extraEnd;
    }

    private static int SkipIdentifier(string text, int start, int end)
    {
        if (start >= end || !CodeLexer.IsIdentifierStart(text, start))
            return start;

        int position = start;
        while (position < end && CodeLexer.IsIdentifierPart(text, position))
            position += char.IsSurrogatePair(text, position) ? 2 : 1;

        return position;
    }

    private static int SkipWhitespace(string text, int start, int end)
    {
        int position = start;
        while (position < end && text[position] is ' ' or '\t')
            position++;

        return position;
    }
}
