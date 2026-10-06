using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Structure and reuse: deep nesting, long option bodies, repeated lines and repeated colors.</summary>
internal sealed partial class StyleChecks
{
    /// <summary>PIB5001: reports the first line of each block nested deeper than the limit, and nothing further inside it.</summary>
    private void CheckNesting(IReadOnlyList<StatementSyntax> block, int depth, int limit)
    {
        if (depth > limit)
        {
            Report(DiagnosticCatalog.DeepNesting, FirstLine(block[0]), depth, limit);
            return;
        }

        foreach (IReadOnlyList<StatementSyntax> inner in block.SelectMany(SyntaxWalk.BlocksOf).Where(inner => inner.Count > 0))
            CheckNesting(inner, depth + 1, limit);
    }

    /// <summary>PIB5002: counts the lines of an option's body that hold something, leaving out blank lines and comments.</summary>
    private void CheckOptionBody(OptionSyntax option)
    {
        if (option.Body.Count == 0)
            return;

        int first = LineOf(option.Body[0].Span.Start);
        int lines = Enumerable.Range(first, LineOf(option.Body[^1].Span.End) - first + 1).Count(HoldsContent);
        int limit = Settings.Threshold("pibbles_max_option_body");
        if (lines > limit)
            Report(DiagnosticCatalog.LongOptionBody, new(option.Span.Start, 2), lines, limit);
    }

    /// <summary>
    /// PIB5003 and PIB5004: the same speaker saying the same text, and the same <c>[color]</c> value, across the story.
    /// Each place is reported when the count reaches its own file's threshold.
    /// </summary>
    private void CheckRepeats(IReadOnlyList<SyntaxTree> trees)
    {
        List<(SyntaxTree Tree, TextLineSyntax Line, string Text)> lines = [];
        List<(SyntaxTree Tree, StringLiteralSyntax Color)> colors = [];
        foreach (SyntaxTree tree in trees)
        {
            Tree = tree;
            foreach (StatementSyntax statement in tree.Root.Nodes.SelectMany(node => SyntaxWalk.Statements(node.Body)))
            {
                if (statement is TextLineSyntax { Content.Count: > 0 } line)
                    lines.Add((tree, line, TextOf(SpanOf(line.Content))));

                colors.AddRange(SyntaxWalk.TextOf(statement)
                    .SelectMany(SyntaxWalk.Runs)
                    .SelectMany(run => run.OfType<MarkupSyntax>())
                    .Select(ColorOf)
                    .OfType<StringLiteralSyntax>()
                    .Select(color => (tree, color)));
            }
        }

        foreach (var repeats in lines.GroupBy(entry => (entry.Line.Speaker?.Text, entry.Text)))
        {
            foreach (var (tree, line, _) in repeats.Where(entry => repeats.Count() >= SettingsOf(entry.Tree).Threshold("pibbles_min_repeated_lines")))
            {
                object who = line.Speaker is { } speaker ? new SpeakerMention(speaker.Text, "{0} says this same line") : "This same narration appears";
                ReportAt(DiagnosticCatalog.RepeatedLine, tree.Source.GetLocation(SpanOf(line.Content)), who, repeats.Count());
            }
        }

        foreach (var repeats in colors.GroupBy(entry => entry.Color.Value.ToLowerInvariant()))
        {
            foreach (var (tree, color) in repeats.Where(entry => repeats.Count() >= SettingsOf(entry.Tree).Threshold("pibbles_min_repeated_colors")))
                ReportAt(DiagnosticCatalog.RepeatedColor, tree.Source.GetLocation(color.Span), color.Value, repeats.Count());
        }
    }

    /// <summary>The value of a <c>[color]</c> span, when it's written out as text.</summary>
    private static StringLiteralSyntax? ColorOf(MarkupSyntax markup) =>
        markup.Name.Text is "color"
            ? markup.Arguments.FirstOrDefault(argument => argument.Name is null or { Text: "value" })?.Value as StringLiteralSyntax
            : null;
}
