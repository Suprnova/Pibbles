using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>Text lines, choices and options: the lines whose content is mostly inline text.</summary>
internal sealed partial class Parser
{
    /// <summary>
    /// Whose text the line being parsed holds, for messages: <c>the text</c>, <c>the option's text</c>, or a
    /// <see cref="SpeakerMention"/> that reads <c>what Mira says</c> once the speaker's display name is known.
    /// </summary>
    private object textOwner = "the text";

    /// <summary>Where the content of the line being parsed starts, for messages that rewrite the line.</summary>
    private int textLineStart;

    private TextLineSyntax ParseTextLine(SourceLine line)
    {
        lineFailed = false;
        inlineEnd = ContentEnd(line);
        int start = line.Content.Start;
        textOwner = "the text";
        textLineStart = start;
        ReportCommentLookalike(start);

        SpeakerSyntax? speaker = null;
        int textStart = start;
        if (SpeakerScanner.Scan(source.Text, start, inlineEnd) is { IsSpeaker: true } match)
        {
            NameSyntax? name = match.Name is { } nameSpan ? new(TextOf(nameSpan)) { Span = nameSpan } : null;
            IReadOnlyList<NameSyntax> values = match.Values.Count == 0 ? [] : [.. match.Values.Select(span => new NameSyntax(TextOf(span)) { Span = span })];
            speaker = new(name, values) { Span = new(start, (match.Parentheses?.End ?? match.Name!.Value.End) - start) };
            if (name is not null)
                textOwner = new SpeakerMention(name.Text, "what {0} says");

            textStart = match.Colon + 1;
        }

        List<InlineSyntax> content = ParseInlineText(SkipWhitespace(textStart), inlineEnd, option: false);
        List<TagSyntax> tags = ParseTrailingTags();
        ReportDeferredUnclosed();

        if (speaker is { Name: { } speakerName, Values.Count: 0 } && content.Count == 0)
            Fail(DiagnosticCatalog.EmptyLine, new(start, textStart - start), speakerName.Text, new SpeakerMention(speakerName.Text));
        else if (speaker is null && content.Count == 0 && tags is [{ Span.Start: int first }, ..])
        {
            TextSpan span = new(first, tags[^1].Span.End - first);
            Fail(DiagnosticCatalog.TagsOnlyLine, span, TextOf(span));
        }

        CheckTags(tags, speaker is { Values.Count: > 0 } && content.Count == 0 ? TagPlace.ValuesOnly : TagPlace.TextLine);

        index++;
        return new(speaker, content, tags) { Span = new(start, inlineEnd - start) };
    }

    private ChoiceSyntax ParseChoice(SourceLine first)
    {
        List<OptionSyntax> options = [ParseOption(first)];
        while (Current is { Kind: LineTokenKind.Line, Line.Kind: LineKind.Option })
            options.Add(ParseOption(Current.Line));

        int start = options[0].Span.Start;
        return new(options) { Span = new(start, options[^1].Span.End - start) };
    }

    /// <summary>Parses an option: its text, then its modifiers (<c>@if</c>, <c>@once</c>), then its tags, then the block under it.</summary>
    private OptionSyntax ParseOption(SourceLine line)
    {
        lineFailed = false;
        int lineEnd = inlineEnd = ContentEnd(line);
        int start = line.Content.Start;
        textOwner = "the option's text";
        textLineStart = start;

        List<InlineSyntax> text = ParseInlineText(SkipWhitespace(start + 2), inlineEnd, option: true);
        ExpressionSyntax? condition = null;
        bool isOnce = false;
        List<TagSyntax> tags;

        if (AtModifierStart())
        {
            StartCode(inlinePosition, new(inlinePosition, 0));
            while (token.Kind is TokenKind.AtWord && !lineFailed)
            {
                string modifier = CodeOf(token.Span);
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
                    FailUnexpected(token.Span, modifier is not ("@if" or "@once") && condition is null && !isOnce ? DiagnosticCatalog.EscapeHelp(TextOf(token.Span)) : null);
                }
            }

            tags = ReadTags();
            if (token.Kind is not TokenKind.EndOfLine)
            {
                if (tags.Count > 0)
                {
                    ReportTextAfterTag(tags[0]);
                    tags = [];
                }
                else
                {
                    FailUnexpected(token.Span);
                }
            }
        }
        else
        {
            tags = ParseTrailingTags();
        }

        ReportDeferredUnclosed();
        CheckTags(tags, TagPlace.Option);
        index++;

        List<StatementSyntax> body = ParseIndentedStatements() ?? [];

        int end = body.Count > 0 ? body[^1].Span.End : lineEnd;
        return new(text, condition, isOnce, tags, body) { Span = new(start, end - start) };
    }

    /// <summary>Reports text after a tag, with the line rewritten so the tag's <c>#</c> is escaped, as the fix.</summary>
    private void ReportTextAfterTag(TagSyntax tag)
    {
        string before = source.Text[textLineStart..tag.Span.Start];
        string after = Whitespace.TrimEnd(source.Text.AsSpan(tag.Span.Start, inlineEnd - tag.Span.Start)).ToString();
        Fail(DiagnosticCatalog.TextAfterTag, tag.Span, TextOf(tag.Span), textOwner, $"{before}\\{after}");
    }
}
