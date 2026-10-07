using System.Text;
using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// Inline text: plain text and escapes, read character by character, with <c>[…]</c> and <c>{…}</c> read in code mode by
/// the same lexer and expression parser as <c>@</c> lines. Reading stops at the end of the region, at a tag, at an
/// option's first modifier, at a markup close that belongs to an enclosing span, and at an <c>{elif}</c>, <c>{else}</c>
/// or <c>{/…}</c> boundary.
/// </summary>
internal sealed partial class Parser
{
    private int inlinePosition;
    private int inlineEnd;
    private bool inOption;
    private List<string> openMarkup = [];
    private (string Name, TextSpan Span, int End)? pendingClose;

    /// <summary>Parses the inline text of one line, from <paramref name="start"/> to where its tags or modifiers begin, with trailing whitespace trimmed.</summary>
    private List<InlineSyntax> ParseInlineText(int start, int end, bool option)
    {
        inlinePosition = start;
        inlineEnd = end;
        inOption = option;
        openMarkup = [];
        pendingClose = null;

        List<InlineSyntax> items = [];
        while (true)
        {
            items.AddRange(ParseInlineItems());
            if (!AtInline('{') || BraceKeyword() is not ({ } and not "if", _))
                break;

            int close = FindCloser('}', inlinePosition);
            var stray = new TextSpan(inlinePosition, close - inlinePosition);
            Fail(DiagnosticCatalog.Unexpected, stray, $"`{TextOf(stray)}`");
            inlinePosition = close;
        }

        if (items is [.., TextRunSyntax last])
        {
            string trimmed = last.Text.TrimEnd(' ', '\t');
            items.RemoveAt(items.Count - 1);
            if (trimmed.Length > 0)
                items.Add(new TextRunSyntax(trimmed) { Span = last.Span with { Length = last.Span.Length - (last.Text.Length - trimmed.Length) } });
        }

        return items;
    }

    private List<InlineSyntax> ParseInlineItems()
    {
        List<InlineSyntax> items = [];
        var text = new StringBuilder();
        int textStart = inlinePosition;

        while (inlinePosition < inlineEnd && !AtTagStart() && !AtModifierStart())
        {
            char c = source.Text[inlinePosition];
            if (c is '\\')
            {
                ReadEscape(text);
                continue;
            }

            if (c is not ('[' or '{'))
            {
                if (c is '\uFFFC')
                    Fail(DiagnosticCatalog.ObjectReplacementCharacter, new(inlinePosition, 1));

                text.Append(c);
                inlinePosition++;
                continue;
            }

            if (c is '{' && BraceKeyword().Word is "elif" or "else" or "/")
                break;

            Flush(items, text, textStart);
            if (c is '[' && IsMarkupClose())
            {
                if (ReadMarkupClose())
                    break;
            }
            else
            {
                items.Add(c is '[' ? ParseMarkup() : BraceKeyword().Word is "if" ? ParseConditionalText() : ParsePoint());
            }

            textStart = inlinePosition;
        }

        Flush(items, text, textStart);
        return items;
    }

    private void Flush(List<InlineSyntax> items, StringBuilder text, int textStart)
    {
        if (text.Length == 0)
            return;

        items.Add(new TextRunSyntax(text.ToString()) { Span = new(textStart, inlinePosition - textStart) });
        text.Clear();
    }

    private void ReadEscape(StringBuilder text)
    {
        int start = inlinePosition++;
        if (inlinePosition >= inlineEnd)
        {
            Fail(DiagnosticCatalog.InvalidEscape, new(start, 1), "\\");
            return;
        }

        int length = char.IsSurrogatePair(source.Text, inlinePosition) ? 2 : 1;
        string escaped = source.Text.Substring(inlinePosition, length);
        if (!CodeLexer.IsEscapable(escaped[0]))
            Fail(DiagnosticCatalog.InvalidEscape, new(start, length + 1), "\\" + escaped);

        text.Append(escaped);
        inlinePosition += length;
    }

    private MarkupSyntax ParseMarkup()
    {
        int open = inlinePosition;
        StartCode(open + 1, new(open, 1));
        NameSyntax name = ExpectName("a markup name", "[");
        List<ArgumentSyntax> arguments = ParseArguments(allowWait: false, out _);
        int tagEnd = CloseCode(TokenKind.CloseBracket, "]", "[", new(open, 1));

        openMarkup.Add(name.Text);
        List<InlineSyntax> content = ParseInlineItems();
        openMarkup.RemoveAt(openMarkup.Count - 1);

        int end = inlinePosition;
        if (pendingClose is not { } close)
        {
            Fail(DiagnosticCatalog.UnclosedMarkup, new(open, tagEnd - open), name.Text);
        }
        else if (close.Name == name.Text)
        {
            inlinePosition = end = close.End;
            pendingClose = null;
        }
        else
        {
            Fail(DiagnosticCatalog.MarkupOutOfOrder, close.Span, name.Text);
            end = close.Span.Start;
        }

        return new MarkupSyntax(name, arguments, content) { Span = new(open, end - open) };
    }

    private bool IsMarkupClose() => SkipWhitespace(inlinePosition + 1) is var slash && slash < inlineEnd && source.Text[slash] is '/';

    /// <summary>
    /// Reads <c>[/name]</c>. A close that belongs to an open span is left for that span, and returns <see langword="true"/>.
    /// A close with no open span is reported and skipped.
    /// </summary>
    private bool ReadMarkupClose()
    {
        int open = inlinePosition;
        int slash = SkipWhitespace(open + 1);
        StartCode(slash + 1, new(open, slash + 1 - open));
        NameSyntax name = ExpectName("a markup name", "[/");
        int end = CloseCode(TokenKind.CloseBracket, "]", "[/", new(open, slash + 1 - open));
        var span = new TextSpan(open, end - open);

        if (!name.IsMissing && openMarkup.Contains(name.Text))
        {
            inlinePosition = open;
            pendingClose = (name.Text, span, end);
            return true;
        }

        if (!name.IsMissing)
            Fail(DiagnosticCatalog.UnopenedMarkup, span, name.Text);

        return false;
    }

    private InlineSyntax ParsePoint()
    {
        int open = inlinePosition;
        var openSpan = new TextSpan(open, 1);
        StartCode(open + 1, openSpan);

        (InlineSyntax point, string? optionForm) = ReadPointBody(open);
        CloseCode(TokenKind.CloseBrace, "}", "{", openSpan);
        point = point with { Span = new(open, inlinePosition - open) };

        if (inOption && optionForm is not null)
            Fail(DiagnosticCatalog.NotInOption, point.Span, optionForm);

        return point;
    }

    /// <summary>Reads what's inside <c>{…}</c>. Returns the point, and how to name it if it's one that option text can't hold.</summary>
    private (InlineSyntax Point, string? OptionForm) ReadPointBody(int open)
    {
        Token first = token;
        string text = TextOf(first.Span);
        switch (first.Kind)
        {
            case TokenKind.Variable:
                Advance();
                return (new InterpolationSyntax(new VariableExpressionSyntax(text[1..]) { Span = first.Span }) { Span = default }, null);

            case TokenKind.AtWord:
                Advance();
                var command = new NameSyntax(text[1..]) { Span = new(first.Span.Start + 1, first.Span.Length - 1) };
                List<ArgumentSyntax> arguments = ParseArguments(allowWait: true, out CommandWait wait);
                return (new InlineCommandSyntax(command, arguments, wait) { Span = default }, $"{{{text}}}");

            case TokenKind.Name when text is "w":
                Advance();
                ExpressionSyntax? duration = token.Kind is TokenKind.CloseBrace or TokenKind.EndOfLine ? null : ParseArgumentValue();
                return (new PauseSyntax(duration) { Span = default }, "{w}");

            case TokenKind.Name when text is "speed":
                Advance();
                ExpressionSyntax? factor = token.Kind is TokenKind.CloseBrace or TokenKind.EndOfLine ? null : ParseArgumentValue();
                return (new SpeedSyntax(factor) { Span = default }, "{speed}");

            case TokenKind.Name when text is "p":
                Advance();
                return (new PageBreakSyntax { Span = default }, "{p}");

            case TokenKind.Name when text is "br":
                Advance();
                return (new LineBreakSyntax { Span = default }, null);

            case TokenKind.Name when text is "icon":
                Advance();
                return (new IconSyntax(ExpectName("an icon name", "{icon")) { Span = default }, null);

            case TokenKind.Name:
                Advance();
                if (token.Kind is TokenKind.CallOpen or TokenKind.OpenParen)
                    return (new InterpolationSyntax(ParseNameValue(first, argument: false)) { Span = default }, null);

                Fail(DiagnosticCatalog.UnknownPoint, PointSpan(open), TextOf(PointSpan(open)));
                return (new InterpolationSyntax(new NameExpressionSyntax(text) { Span = first.Span }) { Span = default }, null);

            default:
                Fail(DiagnosticCatalog.UnknownPoint, PointSpan(open), TextOf(PointSpan(open)));
                return (new InterpolationSyntax(new ErrorExpressionSyntax { Span = new(open + 1, 0) }) { Span = default }, null);
        }
    }

    private TextSpan PointSpan(int open) => new(open, FindCloser('}', open) - open);

    private ConditionalTextSyntax ParseConditionalText()
    {
        int open = inlinePosition;
        var openSpan = new TextSpan(open, BraceKeyword().End - open);
        ExpressionSyntax condition = ParseBranchCondition(openSpan);
        List<InlineSyntax> content = ParseBranchContent();

        List<ElseIfTextSyntax> elseIfs = [];
        ElseTextSyntax? @else = null;
        bool closed = false;
        while (AtInline('{') && !closed)
        {
            int branch = inlinePosition;
            (string? word, int keywordEnd) = BraceKeyword();
            var keywordSpan = new TextSpan(branch, keywordEnd - branch);
            if (word is "elif" && @else is null)
            {
                ExpressionSyntax branchCondition = ParseBranchCondition(keywordSpan);
                List<InlineSyntax> branchContent = ParseBranchContent();
                elseIfs.Add(new(branchCondition, branchContent) { Span = new(branch, inlinePosition - branch) });
            }
            else if (word is "else" && @else is null)
            {
                StartCode(keywordEnd, keywordSpan);
                CloseCode(TokenKind.CloseBrace, "}", "{else", keywordSpan);
                List<InlineSyntax> branchContent = ParseBranchContent();
                @else = new(branchContent) { Span = new(branch, inlinePosition - branch) };
            }
            else if (word is "/")
            {
                StartCode(keywordEnd, keywordSpan);
                if (IsWord("if"))
                    Advance();
                else
                    Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "`if`", "{/");

                CloseCode(TokenKind.CloseBrace, "}", "{/", keywordSpan);
                closed = true;
            }
            else
            {
                break;
            }
        }

        if (!closed)
            Fail(DiagnosticCatalog.Unclosed, openSpan, "{/if}", "{if}");

        return new(condition, content, elseIfs, @else) { Span = new(open, inlinePosition - open) };
    }

    private ExpressionSyntax ParseBranchCondition(TextSpan keyword)
    {
        StartCode(keyword.End, keyword);
        ExpressionSyntax condition = ParseExpression();
        CloseCode(TokenKind.CloseBrace, "}", TextOf(keyword), keyword);
        return condition;
    }

    /// <summary>Parses one branch of conditional text. Markup can't cross a branch boundary, so each branch starts with no open spans.</summary>
    private List<InlineSyntax> ParseBranchContent()
    {
        List<string> outer = openMarkup;
        openMarkup = [];
        List<InlineSyntax> content = ParseInlineItems();
        openMarkup = outer;
        return content;
    }

    /// <summary>
    /// Reads the tags that end a line, from the inline position. Anything after them is reported. Text after a tag means
    /// the tag was probably meant as text, so the line then has no tags.
    /// </summary>
    private List<TagSyntax> ParseTrailingTags()
    {
        List<TagSyntax> tags = [];
        for (int position = SkipWhitespace(inlinePosition); position < inlineEnd; position = SkipWhitespace(inlinePosition))
        {
            inlinePosition = position;
            if (!AtTagStart())
            {
                var rest = new TextSpan(position, inlineEnd - position);
                if (tags.Count == 0)
                {
                    Fail(DiagnosticCatalog.Unexpected, rest, $"`{TextOf(rest).TrimEnd()}`");
                    break;
                }

                ReportTextAfterTag(tags[0]);
                return [];
            }

            Token tag = new CodeLexer(source, new(position, inlineEnd - position), diagnostics).Next();
            tags.Add(TagFrom(tag));
            inlinePosition = tag.Span.End;
        }

        return tags;
    }

    /// <summary>Starts reading code mode at <paramref name="start"/>, up to the end of the inline region.</summary>
    private void StartCode(int start, TextSpan previous)
    {
        lexer = new(source, new(start, inlineEnd - start), diagnostics);
        previousEnd = previous.End;
        previousSpan = previous;
        token = lexer.Next();
    }

    /// <summary>
    /// Ends a stretch of code mode at its closing <c>]</c> or <c>}</c>, and returns inline reading to just after it. When the
    /// closer is missing, reports it and resumes after the next closer character, or at the end of the region.
    /// </summary>
    private int CloseCode(TokenKind closer, string closerText, string opener, TextSpan openSpan)
    {
        if (token.Kind == closer)
        {
            inlinePosition = token.Span.End;
            return inlinePosition;
        }

        int? resume = NextCloser(closerText[0], previousEnd);
        if (token.Kind is TokenKind.EndOfLine || resume is null)
            Fail(DiagnosticCatalog.Unclosed, openSpan, closerText, opener);
        else
            Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{TextOf(token.Span)}`");

        inlinePosition = resume ?? inlineEnd;
        return inlinePosition;
    }

    /// <summary>The position just after the next <paramref name="closer"/> from <paramref name="from"/>, or the end of the region.</summary>
    private int FindCloser(char closer, int from) => NextCloser(closer, from) ?? inlineEnd;

    /// <summary>The position just after the next <paramref name="closer"/> from <paramref name="from"/>, or <see langword="null"/> if the region has none.</summary>
    private int? NextCloser(char closer, int from)
    {
        int index = from < inlineEnd ? source.Text.IndexOf(closer, from, inlineEnd - from) : -1;
        return index < 0 ? null : index + 1;
    }

    /// <summary>The keyword fused with the <c>{</c> at the inline position (<c>if</c>, <c>elif</c>, <c>else</c> or <c>/</c>), and where it ends.</summary>
    private (string? Word, int End) BraceKeyword()
    {
        int start = SkipWhitespace(inlinePosition + 1);
        if (start < inlineEnd && source.Text[start] is '/')
            return ("/", start + 1);

        int end = start;
        while (end < inlineEnd && CodeLexer.IsIdentifierPart(source.Text, end))
            end++;

        string word = source.Text[start..end];
        return word is "if" or "elif" or "else" ? (word, end) : (null, start);
    }

    private bool AtInline(char c) => inlinePosition < inlineEnd && source.Text[inlinePosition] == c;

    private bool AtTagStart() =>
        AtInline('#') && inlinePosition + 1 < inlineEnd && char.IsLetter(source.Text, inlinePosition + 1);

    private bool AtModifierStart() =>
        inOption && AtInline('@') && inlinePosition + 1 < inlineEnd && CodeLexer.IsIdentifierStart(source.Text, inlinePosition + 1);

    private int SkipWhitespace(int position)
    {
        while (position < inlineEnd && source.Text[position] is ' ' or '\t')
            position++;

        return position;
    }
}
