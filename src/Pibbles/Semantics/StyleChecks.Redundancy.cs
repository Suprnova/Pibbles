using System.Globalization;
using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Redundancy: pose changes nobody sees, pauses and markup that do nothing, conditions that make no difference, and a last <c>@return</c>.</summary>
internal sealed partial class StyleChecks
{
    /// <summary>
    /// PIB5010: follows each actor's pose down a block, but only while that's certain. Text lines, <c>@set</c> and
    /// <c>@wait</c> keep what's known; anything else, such as a command, a call or a choice, might change a pose or skip
    /// ahead, so it forgets everything.
    /// </summary>
    private void CheckPoses(IReadOnlyList<StatementSyntax> block)
    {
        Dictionary<string, string> poses = [];
        Dictionary<string, TextLineSyntax> unseen = [];
        foreach (StatementSyntax statement in block)
        {
            switch (statement)
            {
                case TextLineSyntax { Speaker: { } speaker } line when symbols.Actors.ContainsKey(speaker.Text):
                    CheckPose(line, speaker.Text, poses, unseen);
                    break;

                case TextLineSyntax or WaitStatementSyntax:
                    unseen.Clear();
                    break;

                case SetStatementSyntax:
                    break;

                default:
                    poses.Clear();
                    unseen.Clear();
                    break;
            }

            foreach (IReadOnlyList<StatementSyntax> inner in SyntaxWalk.BlocksOf(statement))
                CheckPoses(inner);
        }
    }

    /// <summary>Reports a pose the actor already has, or a pose-only line whose pose changes again before any line shows.</summary>
    private void CheckPose(TextLineSyntax line, string actor, Dictionary<string, string> poses, Dictionary<string, TextLineSyntax> unseen)
    {
        if (line.Pose is { } pose)
        {
            if (unseen.Remove(actor, out TextLineSyntax? hidden))
                Report(DiagnosticCatalog.RedundantPose, hidden.Span, new SpeakerMention(actor, "{0}'s pose changes again before a line shows"), "Remove this line.");
            else if (poses.GetValueOrDefault(actor) == pose.Text)
                Report(DiagnosticCatalog.RedundantPose, pose.Span, new SpeakerMention(actor, "{0} already has that pose"), "Leave the pose out of this line.");

            poses[actor] = pose.Text;
        }

        if (line.Content.Count > 0)
            unseen.Clear();
        else if (line.Pose is not null)
            unseen[actor] = line;
    }

    /// <summary>PIB5011: <c>{w}</c> at the end of a line or before <c>{p}</c>, and timed pauses side by side.</summary>
    private void CheckPauses(IReadOnlyList<InlineSyntax> content)
    {
        if (content is [.., PauseSyntax { Duration: null } last])
            Report(DiagnosticCatalog.RedundantPause, last.Span, "does nothing, because the end of the line already waits for a click", "Remove it.");

        foreach (IReadOnlyList<InlineSyntax> run in SyntaxWalk.Runs(content))
        {
            for (int i = 0; i + 1 < run.Count; i++)
            {
                switch (run[i], run[i + 1])
                {
                    case (PauseSyntax { Duration: null } pause, PageBreakSyntax):
                        Report(DiagnosticCatalog.RedundantPause, pause.Span, "does nothing, because the `{p}` after it already waits", "Remove it.");
                        break;

                    case (PauseSyntax { Duration: { } first } pause, PauseSyntax { Duration: { } second } next):
                        Report(DiagnosticCatalog.RedundantPause, new(pause.Span.Start, next.Span.End - pause.Span.Start), "comes right after another one, so the two could be one", CombinedPause(first, second));
                        break;
                }
            }
        }
    }

    private static string CombinedPause(ExpressionSyntax first, ExpressionSyntax second) =>
        Seconds(first) is { } a && Seconds(second) is { } b
            ? $"Write one pause: `{{w {(a + b).ToString(CultureInfo.InvariantCulture)}}}`."
            : "Write them as one pause.";

    private static decimal? Seconds(ExpressionSyntax duration) => duration switch
    {
        NumberLiteralSyntax number => (decimal)number.Value,
        DurationLiteralSyntax literal => (decimal)literal.Seconds,
        _ => null,
    };

    /// <summary>
    /// PIB5012: empty spans, a span inside one just like it, and two like spans side by side. Spans are alike when they
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
                Report(DiagnosticCatalog.RedundantMarkup, Opening(markup), name, "is empty", "Remove it.");
            else if (outer.Contains(key))
                Report(DiagnosticCatalog.RedundantMarkup, Opening(markup), name, "is inside another one just like it, so it changes nothing", $"Remove the inner `[{name}]` and its `[/{name}]`.");
            else if (i > 0 && run[i - 1] is MarkupSyntax previous && MarkupKey(previous) == key)
                Report(DiagnosticCatalog.RedundantMarkup, Opening(markup), name, "starts right where one just like it ends, so the two could be one", "Join them into one span.");

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
    /// blank lines and comments.
    /// </summary>
    private void CheckBranches(IfStatementSyntax @if)
    {
        if (@if.Else is not { } @else)
            return;

        IReadOnlyList<StatementSyntax>[] branches = [@if.Body, .. @if.ElseIfs.Select(elseIf => elseIf.Body), @else.Body];
        if (branches.All(branch => branch.Count > 0) && branches.Select(BlockText).Distinct().Count() == 1)
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
