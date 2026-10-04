using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>Text lines, choices and options: the lines whose content is mostly inline text.</summary>
internal sealed partial class Parser
{
    private TextLineSyntax ParseTextLine(SourceLine line)
    {
        lineFailed = false;
        inlineEnd = line.Content.End;
        int start = line.Content.Start;

        NameSyntax? speaker = null;
        NameSyntax? pose = null;
        int textStart = start;
        if (MatchSpeaker(start) is { } match)
        {
            speaker = new(TextOf(match.Speaker)) { Span = match.Speaker };
            pose = match.Pose is { } poseSpan ? new(TextOf(poseSpan)) { Span = poseSpan } : null;
            textStart = match.End;
        }

        List<InlineSyntax> content = ParseInlineText(SkipWhitespace(textStart), line.Content.End, option: false);
        List<TagSyntax> tags = ParseTrailingTags();

        if (speaker is not null && pose is null && content.Count == 0)
            Fail(DiagnosticCatalog.EmptyLine, new(start, textStart - start), speaker.Text);

        index++;
        return new(speaker, pose, content, tags) { Span = new(start, TrimmedEnd(line) - start) };
    }

    /// <summary>
    /// Matches a speaker at the start of a text line: <c>name:</c> or <c>name (pose):</c>, with any spacing, followed by
    /// whitespace or the end of the line. Anything else makes the whole line narration.
    /// </summary>
    private (TextSpan Speaker, TextSpan? Pose, int End)? MatchSpeaker(int start)
    {
        int speakerEnd = SkipIdentifier(start);
        if (speakerEnd == start)
            return null;

        int position = SkipWhitespace(speakerEnd);
        TextSpan? pose = null;
        if (position < inlineEnd && source.Text[position] is '(')
        {
            int poseStart = SkipWhitespace(position + 1);
            int poseEnd = SkipIdentifier(poseStart);
            position = SkipWhitespace(poseEnd);
            if (poseEnd == poseStart || position >= inlineEnd || source.Text[position] is not ')')
                return null;

            pose = new TextSpan(poseStart, poseEnd - poseStart);
            position = SkipWhitespace(position + 1);
        }

        if (position >= inlineEnd || source.Text[position] is not ':')
            return null;

        position++;
        if (position < inlineEnd && source.Text[position] is not (' ' or '\t'))
            return null;

        return (new(start, speakerEnd - start), pose, position);
    }

    private int SkipIdentifier(int start)
    {
        if (start >= inlineEnd || !CodeLexer.IsIdentifierStart(source.Text, start))
            return start;

        int end = start;
        while (end < inlineEnd && CodeLexer.IsIdentifierPart(source.Text, end))
            end += char.IsSurrogatePair(source.Text, end) ? 2 : 1;

        return end;
    }

    private ChoiceSyntax ParseChoice()
    {
        List<OptionSyntax> options = [];
        while (Current is { Kind: LineTokenKind.Line, Line.Kind: LineKind.Option })
            options.Add(ParseOption(Current.Line));

        int start = options[0].Span.Start;
        return new(options) { Span = new(start, options[^1].Span.End - start) };
    }

    /// <summary>Parses an option: its text, then its modifiers (<c>@if</c>, <c>@once</c>), then its tags, then the block under it.</summary>
    private OptionSyntax ParseOption(SourceLine line)
    {
        lineFailed = false;
        inlineEnd = line.Content.End;
        int start = line.Content.Start;

        List<InlineSyntax> text = ParseInlineText(SkipWhitespace(start + 2), line.Content.End, option: true);
        ExpressionSyntax? condition = null;
        bool isOnce = false;
        List<TagSyntax> tags;

        if (AtModifierStart())
        {
            StartCode(inlinePosition, new(inlinePosition, 0));
            while (token.Kind is TokenKind.AtWord && !lineFailed)
            {
                string modifier = TextOf(token.Span);
                if (modifier is "@if" && condition is null)
                {
                    Advance();
                    condition = ParseExpression();
                }
                else if (modifier is "@once" && !isOnce)
                {
                    Advance();
                    isOnce = true;
                }
                else
                {
                    Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{modifier}`");
                }
            }

            tags = [];
            while (token.Kind is TokenKind.Tag && !lineFailed)
                tags.Add(ReadTag());

            if (token.Kind is not TokenKind.EndOfLine)
            {
                if (tags.Count > 0)
                    Fail(DiagnosticCatalog.TextAfterTag, tags[0].Span, TextOf(tags[0].Span));
                else
                    Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{TextOf(token.Span)}`");
            }
        }
        else
        {
            tags = ParseTrailingTags();
        }

        int lineEnd = TrimmedEnd(line);
        index++;

        List<StatementSyntax> body = [];
        if (Current.Kind is LineTokenKind.Indent)
        {
            index++;
            body = ParseStatements(nodeLevel: false);
            SkipDedent();
        }

        int end = body.Count > 0 ? Math.Max(body[^1].Span.End, lineEnd) : lineEnd;
        return new(text, condition, isOnce, tags, body) { Span = new(start, end - start) };
    }

    private int TrimmedEnd(SourceLine line) =>
        line.Content.Start + source.Text.AsSpan(line.Content.Start, line.Content.Length).TrimEnd(" \t").Length;

    /// <summary>
    /// Reads arguments up to the end of the line or a closing <c>]</c> or <c>}</c>: positional ones, then named ones,
    /// then, where <paramref name="allowWait"/>, <c>wait</c> or <c>nowait</c>.
    /// </summary>
    private List<ArgumentSyntax> ParseArguments(bool allowWait, out CommandWait wait)
    {
        List<ArgumentSyntax> arguments = [];
        wait = CommandWait.Default;
        while (!AtLineEnd && token.Kind is not (TokenKind.CloseBracket or TokenKind.CloseBrace) && !lineFailed)
        {
            if (token.Kind is TokenKind.Name && TextOf(token.Span) is "wait" or "nowait")
            {
                if (!allowWait)
                {
                    Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{TextOf(token.Span)}`");
                    break;
                }

                wait = TextOf(token.Span) is "wait" ? CommandWait.Wait : CommandWait.NoWait;
                Advance();
                break;
            }

            if (ParseArgument() is not { } argument)
                break;

            if (argument.Name is null && arguments.Any(previous => previous.Name is not null))
                Fail(DiagnosticCatalog.ArgumentOrder, argument.Span);

            arguments.Add(argument);
        }

        return arguments;
    }
}
