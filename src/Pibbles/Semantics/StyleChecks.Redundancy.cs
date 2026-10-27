using System.Globalization;
using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Redundancy: channel values nobody sees, pauses and markup that do nothing, conditions that make no difference, and a last <c>@return</c>.</summary>
internal sealed partial class StyleChecks
{
    /// <summary>
    /// PIB5010 and PIB5016: follows each actor's value in each channel down a block, but only while that's certain. Text
    /// lines, <c>@set</c> and <c>@pause</c> keep what's known; anything else, such as a command, a call or a choice,
    /// might change a value or skip ahead, so it forgets everything.
    /// </summary>
    private void CheckValues(IReadOnlyList<StatementSyntax> block)
    {
        Dictionary<(string Actor, string Channel), string> values = [];
        Dictionary<(string Actor, string Channel), NameSyntax> unseen = [];
        foreach (StatementSyntax statement in block)
        {
            switch (statement)
            {
                case TextLineSyntax { Speaker: { } speaker } line when symbols.Actors.TryGetValue(speaker.Name.Text, out ActorSymbol? actor):
                    CheckLineValues(line, speaker, actor, values, unseen);
                    break;

                case TextLineSyntax or PauseStatementSyntax:
                    unseen.Clear();
                    break;

                case SetStatementSyntax:
                    break;

                default:
                    values.Clear();
                    unseen.Clear();
                    break;
            }

            foreach (IReadOnlyList<StatementSyntax> inner in SyntaxWalk.BlocksOf(statement))
                CheckValues(inner);
        }
    }

    /// <summary>Reports a value the actor already has, or one set by a line with no text that changes again before any line shows.</summary>
    private void CheckLineValues(TextLineSyntax line, SpeakerSyntax speaker, ActorSymbol actor, Dictionary<(string Actor, string Channel), string> values, Dictionary<(string Actor, string Channel), NameSyntax> unseen)
    {
        foreach (NameSyntax value in speaker.Values)
        {
            if (actor.Channels.FirstOrDefault(channel => channel.Values.Any(known => known.Name == value.Text)) is not { } owner)
                continue;

            var key = (actor.Name, owner.Channel.Name);
            if (unseen.Remove(key, out NameSyntax? hidden))
                Report(DiagnosticCatalog.UnseenValueChange, hidden.Span, hidden.Text, new SpeakerMention(actor.Name), owner.Channel.Name);
            else if (values.GetValueOrDefault(key) == value.Text)
                Report(DiagnosticCatalog.RepeatedValue, value.Span, value.Text, new SpeakerMention(actor.Name));

            values[key] = value.Text;
            if (line.Content.Count == 0)
                unseen[key] = value;
        }

        if (line.Content.Count > 0)
            unseen.Clear();
    }

    /// <summary>PIB5011, PIB5017 and PIB5018: <c>{w}</c> at the end of a line or before <c>{page}</c>, and timed pauses side by side.</summary>
    private void CheckPauses(IReadOnlyList<InlineSyntax> content)
    {
        if (content is [.., InputWaitSyntax last])
            Report(DiagnosticCatalog.PauseAtLineEnd, last.Span);

        foreach (IReadOnlyList<InlineSyntax> run in SyntaxWalk.Runs(content))
        {
            for (int i = 0; i + 1 < run.Count; i++)
            {
                switch (run[i], run[i + 1])
                {
                    case (InputWaitSyntax wait, PageBreakSyntax):
                        Report(DiagnosticCatalog.PauseBeforePageBreak, wait.Span);
                        break;

                    case (PauseSyntax pause, PauseSyntax next):
                        ReportWithOptionalHelp(DiagnosticCatalog.AdjacentPauses, new(pause.Span.Start, next.Span.End - pause.Span.Start), CombinedPause(pause.Duration, next.Duration));
                        break;
                }
            }
        }
    }

    private static string? CombinedPause(ExpressionSyntax first, ExpressionSyntax second) =>
        Seconds(first) is { } a && Seconds(second) is { } b && ConstantValue.Add(a, b) is { } sum
            ? sum.ToString("G29", CultureInfo.InvariantCulture)
            : null;

    private static decimal? Seconds(ExpressionSyntax duration) => duration switch
    {
        NumberLiteralSyntax number => number.Value,
        DurationLiteralSyntax literal => literal.Seconds,
        _ => null,
    };

    /// <summary>
    /// PIB5012, PIB5019 and PIB5050: empty spans, a span inside one just like it, and two like spans side by side. Spans are alike when they
    /// have the same name and the same arguments.
    /// </summary>
    private void CheckMarkup(IReadOnlyList<InlineSyntax> run, IReadOnlySet<string> outer)
    {
        for (int i = 0; i < run.Count; i++)
        {
            if (run[i] is not MarkupSyntax markup)
            {
                foreach (IReadOnlyList<InlineSyntax> inner in SyntaxWalk.InnerRuns(run[i]))
                    CheckMarkup(inner, outer);

                continue;
            }

            string name = markup.Name.Text;
            string key = MarkupKey(markup);
            if (markup.Content.Count == 0)
                Report(DiagnosticCatalog.EmptyMarkup, Opening(markup), name);
            else if (outer.Contains(key))
                Report(DiagnosticCatalog.NestedMarkup, Opening(markup), name);
            else if (i > 0 && run[i - 1] is MarkupSyntax previous && MarkupKey(previous) == key)
                Report(DiagnosticCatalog.AdjacentMarkup, Opening(markup), name);

            CheckMarkup(markup.Content, new HashSet<string>(outer) { key });
        }
    }

    private string MarkupKey(MarkupSyntax markup) => $"{markup.Name.Text}({string.Join(", ", markup.Arguments.Select(argument => TextOf(argument.Span)))})";

    /// <summary>A span's opening bracket, such as <c>[color "#ff8800"]</c>.</summary>
    private TextSpan Opening(MarkupSyntax markup)
    {
        int end = Tree.Source.Text.IndexOf(']', markup.Arguments.Count > 0 ? markup.Arguments[^1].Span.End : markup.Name.Span.End);
        return new(markup.Span.Start, (end < 0 ? markup.Name.Span.End : end + 1) - markup.Span.Start);
    }

    /// <summary>
    /// PIB5013: an <c>@if</c> with an <c>@else</c> whose branches all read the same, apart from indentation, line IDs,
    /// blank lines and comments. Branches that count how often they ran are left alone, since merging them would share one count.
    /// </summary>
    private void CheckBranches(IfStatementSyntax @if)
    {
        if (@if.Else is not { } @else)
            return;

        IReadOnlyList<StatementSyntax>[] branches = [@if.Body, .. @if.ElseIfs.Select(elseIf => elseIf.Body), @else.Body];
        if (branches.All(branch => branch.Count > 0) && !branches.Any(SyntaxWalk.HoldsState) && branches.Select(BlockText).Distinct().Count() == 1)
            Report(DiagnosticCatalog.RedundantCondition, new(@if.Span.Start, @if.Condition.Span.End - @if.Span.Start), "@if");
    }

    /// <summary>PIB5013: conditional text with an <c>{else}</c> whose branches are all written the same.</summary>
    private void CheckBranches(ConditionalTextSyntax conditional)
    {
        if (conditional.Else is not null && SyntaxWalk.Branches(conditional).Select(branch => branch.Count > 0 ? TextOf(SpanOf(branch)) : "").Distinct().Count() == 1)
            Report(DiagnosticCatalog.RedundantCondition, new(conditional.Span.Start, conditional.Condition.Span.End + 1 - conditional.Span.Start), "{if}");
    }

    /// <summary>A block's lines, without the block's own indentation, line IDs, blank lines and comments.</summary>
    private string BlockText(IReadOnlyList<StatementSyntax> block)
    {
        int first = LineOf(block[0].Span.Start);
        int indentation = LineText(first).Length - LineText(first).TrimStart(" \t").Length;
        IEnumerable<string> lines = Enumerable.Range(first, LineOf(block[^1].Span.End) - first + 1)
            .Where(HoldsContent)
            .Select(LineText)
            .Select(line => LineId().Replace(line.Length >= indentation ? line[indentation..] : line.TrimStart(), "").TrimEnd());
        return string.Join('\n', lines);
    }

    /// <summary>PIB5014: <c>@return</c> as the last statement of a node.</summary>
    private void CheckReturn(NodeSyntax node)
    {
        if (node.Body is [.., ReturnStatementSyntax @return])
            Report(DiagnosticCatalog.RedundantReturn, @return.Span);
    }

    [GeneratedRegex(@"[ \t]+#id:[a-z][a-z0-9_]*")]
    private static partial Regex LineId();
}
