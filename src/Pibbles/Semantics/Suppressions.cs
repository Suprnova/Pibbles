using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// The <c>// pibbles-ignore</c> comments in a file, such as <c>// pibbles-ignore PIB5003 PIB5010</c>. One on its own line
/// silences the codes it lists on the next line that isn't a comment, and one above the file's first node silences them
/// in the whole file. Only the style and spelling categories can be silenced, since a mistake gets fixed, not hidden.
/// </summary>
internal sealed class Suppressions
{
    private const string Directive = "pibbles-ignore";

    private readonly HashSet<string> everywhere = [];
    private readonly Dictionary<int, HashSet<string>> byLine = [];

    public static Suppressions Of(SyntaxTree tree)
    {
        var suppressions = new Suppressions();
        int firstNode = tree.Root.Nodes.Count > 0 ? tree.Source.GetLinePosition(tree.Root.Nodes[0].Span.Start).Line : int.MaxValue;
        for (int i = 0; i < tree.Comments.Count; i++)
        {
            Comment comment = tree.Comments[i];
            int line = tree.Source.GetLinePosition(comment.Span.Start).Line;
            if (!comment.OnOwnLine || Codes(comment.Text) is not [_, ..] codes)
                continue;

            if (line < firstNode)
            {
                suppressions.everywhere.UnionWith(codes);
            }
            else
            {
                int target = NextLine(tree, i, line);
                if (!suppressions.byLine.TryGetValue(target, out HashSet<string>? silenced))
                    suppressions.byLine[target] = silenced = [];

                silenced.UnionWith(codes);
            }
        }

        return suppressions;
    }

    /// <summary>Whether one of the file's <c>// pibbles-ignore</c> comments silences a diagnostic.</summary>
    public bool Silences(Diagnostic diagnostic) =>
        diagnostic.Descriptor?.Category is DiagnosticCategory.Style or DiagnosticCategory.Spelling
        && (everywhere.Contains(diagnostic.Code) || byLine.GetValueOrDefault(diagnostic.Location.Start.Line)?.Contains(diagnostic.Code) == true);

    /// <summary>The codes a comment lists after <c>pibbles-ignore</c>, or none if it isn't one.</summary>
    private static string[] Codes(string text) =>
        text.StartsWith(Directive, StringComparison.Ordinal) && text.AsSpan(Directive.Length) is [] or [' ' or '\t', ..]
            ? [.. text[Directive.Length..].Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries).Select(code => code.ToUpperInvariant())]
            : [];

    /// <summary>The first line after <paramref name="line"/> that isn't a comment line, given that the comment at <paramref name="index"/> is on <paramref name="line"/>.</summary>
    private static int NextLine(SyntaxTree tree, int index, int line)
    {
        int next = line + 1;
        for (int i = index + 1; i < tree.Comments.Count && tree.Comments[i] is { OnOwnLine: true } comment && tree.Source.GetLinePosition(comment.Span.Start).Line == next; i++)
            next++;

        return next;
    }
}
