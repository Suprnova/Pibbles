namespace Pibbles.Syntax;

/// <summary>A <c>//</c> comment: on a line of its own, or at the end of any line, after its content.</summary>
/// <remarks>
/// A comment runs to the end of its line and starts at the first <c>//</c> that isn't in a quoted string, in a tag's
/// value or after a backslash. Comments aren't part of the syntax tree, since they never change what a story means.
/// Tools read them, such as <c>// pibbles-ignore</c>, which silences a style hint.
/// </remarks>
/// <param name="Span">The comment, from its <c>//</c> to its last character that isn't whitespace.</param>
/// <param name="Text">What follows the <c>//</c>, without the whitespace around it.</param>
/// <param name="OnOwnLine">Whether only whitespace comes before the comment on its line. A comment after the content of a line isn't.</param>
public sealed record Comment(TextSpan Span, string Text, bool OnOwnLine)
{
    /// <summary>Whether <paramref name="comment"/>, which starts at its <c>//</c>, is a <c>///</c> note, which is only ever a whole line, rather than a comment.</summary>
    internal static bool IsNote(ReadOnlySpan<char> comment) =>
        comment is ['/', '/', '/', ..] and not ['/', '/', '/', '/', ..];

    /// <summary>Reads the comment that starts at <paramref name="span"/>'s <c>//</c> and runs to its end.</summary>
    internal static Comment Read(string text, TextSpan span, bool onOwnLine)
    {
        ReadOnlySpan<char> comment = text.AsSpan(span.Start, span.Length).TrimEnd(" \t");
        return new(new(span.Start, comment.Length), comment[2..].Trim(" \t").ToString(), onOwnLine);
    }
}
