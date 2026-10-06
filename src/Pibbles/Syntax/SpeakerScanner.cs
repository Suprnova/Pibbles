namespace Pibbles.Syntax;

/// <summary>
/// What starts a text line when it looks like a speaker: a name, an optional parenthesis, and a colon, with any spacing
/// between them. It's a speaker only when the parenthesis holds a single name and the colon has whitespace or the end
/// of the line after it. Anything else is narration, which the analyzer checks for lines that almost look like a speaker.
/// </summary>
/// <param name="Name">The name before the parenthesis or colon.</param>
/// <param name="Parentheses">The parenthesis, brackets included, or <see langword="null"/> if there's none.</param>
/// <param name="Pose">The single name inside the parenthesis, or <see langword="null"/> if there's no parenthesis or it holds something else.</param>
/// <param name="Colon">Where the colon is.</param>
/// <param name="IsSpeaker">Whether the line has a speaker, rather than starting with something that looks like one.</param>
internal readonly record struct SpeakerPrefix(TextSpan Name, TextSpan? Parentheses, TextSpan? Pose, int Colon, bool IsSpeaker);

/// <summary>Finds a <see cref="SpeakerPrefix"/> at the start of a text line.</summary>
internal static class SpeakerScanner
{
    /// <summary>Reads what starts the text between <paramref name="start"/> and <paramref name="end"/>, or returns <see langword="null"/> if it isn't a name, then an optional parenthesis, then a colon.</summary>
    public static SpeakerPrefix? Scan(string text, int start, int end)
    {
        int nameEnd = SkipIdentifier(text, start, end);
        if (nameEnd == start)
            return null;

        int position = SkipWhitespace(text, nameEnd, end);
        TextSpan? parentheses = null;
        TextSpan? pose = null;
        if (position < end && text[position] is '(')
        {
            int close = text.IndexOf(')', position, end - position);
            if (close < 0)
                return null;

            parentheses = new TextSpan(position, close + 1 - position);
            int poseStart = SkipWhitespace(text, position + 1, close);
            int poseEnd = SkipIdentifier(text, poseStart, close);
            if (poseEnd > poseStart && SkipWhitespace(text, poseEnd, close) == close)
                pose = new TextSpan(poseStart, poseEnd - poseStart);

            position = SkipWhitespace(text, close + 1, end);
        }

        if (position >= end || text[position] is not ':')
            return null;

        bool spaced = position + 1 >= end || text[position + 1] is ' ' or '\t';
        return new(new(start, nameEnd - start), parentheses, pose, position, spaced && (parentheses is null || pose is not null));
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
