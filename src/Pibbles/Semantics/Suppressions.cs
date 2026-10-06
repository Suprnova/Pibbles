using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// The <c>// pibbles-ignore</c> comments in a file, such as <c>// pibbles-ignore PIB5003 PIB5010</c>. One on its own line
/// silences the codes it lists on the next line that isn't a comment, and one above the file's first node silences them
/// in the whole file. Only style and spelling (PIB5xxx and PIB6xxx) can be silenced, since a mistake gets fixed, not hidden.
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
        foreach (Comment comment in tree.Comments)
        {
            int line = tree.Source.GetLinePosition(comment.Span.Start).Line;
            if (Codes(comment.Text) is not [_, ..] codes || !IsOwnLine(tree.Source, comment))
                continue;

            if (line < firstNode)
            {
                suppressions.everywhere.UnionWith(codes);
            }
            else
            {
                int target = NextLine(tree.Source, line);
                if (!suppressions.byLine.TryGetValue(target, out HashSet<string>? silenced))
                    suppressions.byLine[target] = silenced = [];

                silenced.UnionWith(codes);
            }
        }

        return suppressions;
    }

    /// <summary>Whether one of the file's <c>// pibbles-ignore</c> comments silences a diagnostic.</summary>
    public bool Silences(Diagnostic diagnostic) =>
        diagnostic.Code is ['P', 'I', 'B', '5' or '6', ..]
        && (everywhere.Contains(diagnostic.Code) || byLine.GetValueOrDefault(diagnostic.Location.Start.Line)?.Contains(diagnostic.Code) == true);

    /// <summary>The codes a comment lists after <c>pibbles-ignore</c>, or none if it isn't one.</summary>
    private static string[] Codes(string text) =>
        text.StartsWith(Directive, StringComparison.Ordinal) && text.AsSpan(Directive.Length) is [] or [' ' or '\t', ..]
            ? [.. text[Directive.Length..].Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries).Select(code => code.ToUpperInvariant())]
            : [];

    private static bool IsOwnLine(SourceText source, Comment comment)
    {
        TextSpan line = source.GetLineSpan(source.GetLinePosition(comment.Span.Start).Line);
        return source.Text.AsSpan(line.Start, comment.Span.Start - line.Start).Trim(" \t").IsEmpty;
    }

    private static int NextLine(SourceText source, int line)
    {
        int next = line + 1;
        while (next < source.LineCount && LineClassifier.KindOf(source.Text.AsSpan(source.GetLineSpan(next).Start, source.GetLineSpan(next).Length).TrimStart(" \t")) is LineKind.Comment)
            next++;

        return next;
    }
}
