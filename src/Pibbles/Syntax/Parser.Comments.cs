using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// Finds the comment that ends a text line, an option or a display name, so the line is read up to it and its spans end
/// before it. Lines read by the code-mode lexer end at a comment by themselves.
/// </summary>
internal sealed partial class Parser
{
    private bool lineHasComment;

    /// <summary>What a message about an unclosed bracket, brace or span adds when a comment may have taken the closer.</summary>
    private string CommentHelp => lineHasComment ? DiagnosticCatalog.CommentSwallowedCloserHelp : "";

    /// <summary>
    /// The end of <paramref name="line"/>'s content: before its trailing comment, if it has one, and without trailing
    /// whitespace. A trailing comment is recorded.
    /// </summary>
    private int ContentEnd(SourceLine line)
    {
        int start = line.Content.Start;
        int end = TrimmedEnd(line);
        return EndBeforeComment(start, end, FindComment(start, end, option: line.Kind is LineKind.Option));
    }

    /// <summary>The same for a value that is plain text, such as a display name, where a backslash means nothing.</summary>
    private int PlainContentEnd(int start, int end)
    {
        int index = source.Text.AsSpan(start, end - start).IndexOf("//", StringComparison.Ordinal);
        return EndBeforeComment(start, end, index < 0 ? null : start + index);
    }

    private int EndBeforeComment(int start, int end, int? comment)
    {
        lineHasComment = comment is not null;
        if (comment is not { } at)
            return end;

        if (Comment.IsNote(source.Text.AsSpan(at, end - at)))
            Report(DiagnosticCatalog.UnsupportedNote, new(at, 3));
        else
            comments.Add(Comment.Read(source.Text, new(at, end - at), onOwnLine: false));

        return start + source.Text.AsSpan(start, at - start).TrimEnd(" \t").Length;
    }

    /// <summary>
    /// Finds where the comment on a line of inline text starts. A comment can't start inside a quoted string, a tag's
    /// value or after a backslash, so <c>[…]</c>, <c>{…}</c>, tags and an option's modifiers are read by the code lexer.
    /// </summary>
    private int? FindComment(int start, int end, bool option)
    {
        string text = source.Text;
        if (!text.AsSpan(start, end - start).Contains("//", StringComparison.Ordinal))
            return null;

        for (int i = start; i < end; i++)
        {
            char c = text[i];
            if (c is '\\')
            {
                i++;
                continue;
            }

            if (c is '/' && i + 1 < end && text[i + 1] is '/')
                return i;

            TokenKind? stop = c switch
            {
                '[' => TokenKind.CloseBracket,
                '{' => TokenKind.CloseBrace,
                '#' when i + 1 < end && char.IsLetter(text, i + 1) => TokenKind.Tag,
                '@' when option && i + 1 < end && CodeLexer.IsIdentifierStart(text, i + 1) => TokenKind.EndOfLine,
                _ => null,
            };

            if (stop is not { } kind)
                continue;

            int from = c is '[' or '{' ? i + 1 : i;
            var scratch = new CodeLexer(source, new(from, end - from), []);
            Token token;
            do
            {
                token = scratch.Next();
                if (token.Kind is TokenKind.Comment)
                    return token.Span.Start;
            }
            while (token.Kind != kind && token.Kind is not TokenKind.EndOfLine);

            i = token.Span.End - 1;
        }

        return null;
    }
}
