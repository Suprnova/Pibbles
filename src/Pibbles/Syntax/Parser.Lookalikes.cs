using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// Text that looks like a marker typed through an input method, such as <c>【b】…【/b】</c> for <c>[b]…[/b]</c>, or
/// <c>・・</c> at a line's start for <c>//</c>. It stays text, since text is all it can be read as, and is reported as a
/// warning, so a writer who meant the marker finds out and a writer who meant the text can escape it.
/// </summary>
internal sealed partial class Parser
{
    private const string BracketOpeners = "［「【[";
    private const string BracketClosers = "］」】]";
    private const string Slashes = "/／・";
    private const string BraceClosers = "｝』}";

    private static readonly string[] PointWords = ["w", "page", "br", "pause", "speed", "icon", "if", "elif", "else"];

    /// <summary>The names of <c>「b」</c> and <c>【b】</c> tags on the line that have a closing tag after them, so the closing tag is reported too.</summary>
    private readonly List<string> pairedLookalikes = [];

    private static bool IsLookalikeStart(char c) => c is '［' or '「' or '【' or '｛' or '『' or '＃' or '＠';

    /// <summary>Reports the text at the inline position if it looks like markup, a point, a tag or an option's modifier typed through an input method.</summary>
    private void ReportLookalike()
    {
        int start = inlinePosition;
        int? end = source.Text[start] switch
        {
            '［' => BracketTag(start, anyContent: true)?.End,
            '「' or '【' => PairedBracketTagEnd(start),
            '｛' or '『' => PointLookalikeEnd(start),
            '＃' => TagLookalikeEnd(start),
            '＠' when inOption => ModifierLookalikeEnd(start),
            _ => null,
        };

        if (end is { } at)
            ReportLookalike(new(start, at - start), Halfwidth(start, at));
    }

    /// <summary>
    /// Reports a text line that starts like a comment typed through an input method: two of <c>/</c>, <c>／</c>, <c>・</c> and
    /// <c>、</c>, not both <c>/</c>. With <c>・</c> or <c>、</c>, whitespace or the line's end has to follow, so <c>・・・</c> is text.
    /// </summary>
    private void ReportCommentLookalike(int start)
    {
        ReadOnlySpan<char> text = source.Text.AsSpan(start, inlineEnd - start);
        if (text is not ['/' or '／' or '・' or '、', '/' or '／' or '・' or '、', .. var rest])
            return;

        if (text[..2].ContainsAny('・', '、') && !(rest.IsEmpty || Whitespace.Is(rest[0])))
            return;

        ReportLookalike(new(start, 2), "//");
    }

    private void ReportLookalike(TextSpan span, string marker)
    {
        string written = TextOf(span);
        string help = $"Write `{marker}`, typing markers in your input method's direct (half-width) mode. {DiagnosticCatalog.EscapeHelp(written)}";
        Report(DiagnosticCatalog.MarkerLookalike, span, written, marker, help);
    }

    /// <summary>
    /// Reads a markup tag in any bracket, <c>[b]</c>, <c>「b」</c> or <c>【/b】</c>, with any of <c>/</c>, <c>／</c> or <c>・</c> for a
    /// closing tag's slash. With <paramref name="anyContent"/>, arguments may follow the name, as in <c>［color red］</c>.
    /// </summary>
    private (bool Close, string Name, int End)? BracketTag(int start, bool anyContent)
    {
        int position = SkipWhitespace(start + 1);
        bool close = position < inlineEnd && Slashes.Contains(source.Text[position]);
        if (close)
            position = SkipWhitespace(position + 1);

        int nameEnd = Identifiers.Skip(source.Text, position, inlineEnd);
        if (nameEnd == position)
            return null;

        int closer = anyContent ? source.Text.AsSpan(nameEnd, inlineEnd - nameEnd).IndexOfAny(BracketClosers) + nameEnd : SkipWhitespace(nameEnd);
        return closer >= nameEnd && closer < inlineEnd && BracketClosers.Contains(source.Text[closer]) ? (close, source.Text[position..nameEnd], closer + 1) : null;
    }

    /// <summary>The end of a <c>「b」</c> or <c>【b】</c> tag that has a closing tag later on the line, or of a closing tag whose opening tag did or whose markup is open.</summary>
    private int? PairedBracketTagEnd(int start)
    {
        if (BracketTag(start, anyContent: false) is not { } tag)
            return null;

        if (tag.Close)
            return pairedLookalikes.Remove(tag.Name) || openMarkup.Contains(tag.Name) ? tag.End : null;

        for (int position = tag.End; position < inlineEnd; position++)
        {
            if (source.Text[position] is '\\')
            {
                position++;
                continue;
            }

            if (BracketOpeners.Contains(source.Text[position]) && BracketTag(position, anyContent: false) is { Close: true } close && close.Name == tag.Name)
            {
                pairedLookalikes.Add(tag.Name);
                return tag.End;
            }
        }

        return null;
    }

    /// <summary>The end of a <c>｛…｝</c> or <c>『…』</c> holding what a point holds: a point's word, a variable, a command or a call.</summary>
    private int? PointLookalikeEnd(int start)
    {
        int position = SkipWhitespace(start + 1);
        if (position >= inlineEnd)
            return null;

        int nameEnd = Identifiers.Skip(source.Text, position + 1, inlineEnd);
        bool point = source.Text[position] switch
        {
            '$' or '＄' or '￥' or '@' or '＠' => nameEnd > position + 1,
            '/' or '／' or '・' => Narrowed(position + 1, nameEnd) is "if",
            _ => Identifiers.Skip(source.Text, position, inlineEnd) is var end && end > position
                && (PointWords.Contains(Narrowed(position, end)) || end < inlineEnd && source.Text[end] is '(' or '（'),
        };

        int closer = point ? source.Text.AsSpan(position, inlineEnd - position).IndexOfAny(BraceClosers) : -1;
        return closer < 0 ? null : position + closer + 1;
    }

    /// <summary>The end of a <c>＃tag</c> that only tags follow, to the end of the line, as tags do.</summary>
    private int? TagLookalikeEnd(int start)
    {
        if (TagLikeEnd(start) is not { } end)
            return null;

        for (int position = SkipWhitespace(end); position < inlineEnd; position = SkipWhitespace(position))
        {
            if (TagLikeEnd(position) is not { } next)
                return null;

            position = next;
        }

        return end;
    }

    /// <summary>The end of what reads as one tag at <paramref name="position"/>, a <c>#</c> or <c>＃</c> and a letter up to whitespace, or <see langword="null"/> if none starts there.</summary>
    private int? TagLikeEnd(int position)
    {
        if (position + 1 >= inlineEnd || source.Text[position] is not ('#' or '＃') || !char.IsLetter(source.Text, position + 1))
            return null;

        int end = position + 1;
        while (end < inlineEnd && !Whitespace.Is(source.Text[end]))
            end++;

        return end;
    }

    /// <summary>The end of <c>＠if</c> or <c>＠once</c> after an option's text.</summary>
    private int? ModifierLookalikeEnd(int start)
    {
        int end = Identifiers.Skip(source.Text, start + 1, inlineEnd);
        return Narrowed(start + 1, end) is "if" or "once" ? end : null;
    }

    /// <summary>The text from <paramref name="start"/> to <paramref name="end"/> with full-width letters narrowed, as a keyword it may spell is compared.</summary>
    private string Narrowed(int start, int end) => string.Concat(source.Text.AsSpan(start, end - start).ToArray().Select(MarkerForms.Narrow));

    /// <summary>The text from <paramref name="start"/> to <paramref name="end"/> as the marker it looks like, with every form and full-width letter half-width.</summary>
    private string Halfwidth(int start, int end) =>
        string.Concat(source.Text.AsSpan(start, end - start).ToArray().Select(c => MarkerForms.Map(c) is not '\0' and var marker ? marker : MarkerForms.Narrow(c)));
}
