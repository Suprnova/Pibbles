namespace Pibbles.Syntax;

/// <summary>A <c>//</c> comment: on a line of its own, or after the content of an <c>@</c> line or a node header.</summary>
/// <remarks>
/// Comments aren't part of the syntax tree, since they never change what a story means. Tools read them, such as
/// <c>// pibbles-ignore</c>, which silences a style hint.
/// </remarks>
/// <param name="Span">The comment, from its <c>//</c> to its last character that isn't whitespace.</param>
/// <param name="Text">What follows the <c>//</c>, without the whitespace around it.</param>
public sealed record Comment(TextSpan Span, string Text)
{
    /// <summary>Reads the comment that starts at <paramref name="span"/>'s <c>//</c> and runs to its end.</summary>
    internal static Comment Read(string text, TextSpan span)
    {
        ReadOnlySpan<char> comment = text.AsSpan(span.Start, span.Length).TrimEnd(" \t");
        return new(new(span.Start, comment.Length), comment[2..].Trim(" \t").ToString());
    }
}
