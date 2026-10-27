using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>A line that needs a line ID: its <c>#id</c> tag if it has one, and where a new one goes.</summary>
/// <param name="Id">The line's <c>#id</c> tag, or <see langword="null"/> if it has none.</param>
/// <param name="Insert">Where a new ID goes: after the line's last tag, or its content if it has none, and before a trailing comment.</param>
internal readonly record struct LineIdSite(TagSyntax? Id, int Insert);

/// <summary>
/// Finds the lines that need a line ID: text lines that show text, options, <c>@call</c>, and variation block openers.
/// A line that only sets values shows nothing, so a save never stops on it, and it takes no ID.
/// </summary>
internal static class LineIdSites
{
    public static IEnumerable<LineIdSite> In(SyntaxTree tree)
    {
        Dictionary<int, int> commentStarts = [];
        foreach (Comment comment in tree.Comments.Where(comment => !comment.OnOwnLine))
            commentStarts.TryAdd(tree.Source.GetLinePosition(comment.Span.Start).Line, comment.Span.Start);

        return tree.Root.Nodes.SelectMany(node => In(tree.Source, commentStarts, node.Body));
    }

    private static IEnumerable<LineIdSite> In(SourceText source, Dictionary<int, int> commentStarts, IEnumerable<StatementSyntax> statements) => statements.SelectMany(statement => statement switch
    {
        TextLineSyntax { Speaker.Values.Count: > 0, Content: [] } => [],
        TextLineSyntax line => [new(IdOf(line.Tags), line.Span.End)],
        CallStatementSyntax call => [new(IdOf(call.Tags), call.Span.End)],
        ChoiceSyntax choice => choice.Options.SelectMany(option => (IEnumerable<LineIdSite>)[new(IdOf(option.Tags), LineEnd(source, commentStarts, option.Span.Start)), .. In(source, commentStarts, option.Body)]),
        VariationStatementSyntax variation => [Opener(variation.Span, variation.Tags, variation.Kind is VariationKind.Sequence ? "@sequence" : "@cycle"), .. variation.Alternatives.SelectMany(alternative => In(source, commentStarts, alternative.Body))],
        OnceStatementSyntax once => [Opener(once.Span, once.Tags, "@once"), .. In(source, commentStarts, once.Body)],
        IfStatementSyntax @if => [.. In(source, commentStarts, @if.Body), .. @if.ElseIfs.SelectMany(elseIf => In(source, commentStarts, elseIf.Body)), .. In(source, commentStarts, @if.Else?.Body ?? [])],
        _ => [],
    });

    private static LineIdSite Opener(TextSpan span, IReadOnlyList<TagSyntax> tags, string keyword) =>
        new(IdOf(tags), tags.Count > 0 ? tags[^1].Span.End : span.Start + keyword.Length);

    private static TagSyntax? IdOf(IEnumerable<TagSyntax> tags) => tags.FirstOrDefault(tag => tag.Name is "id");

    /// <summary>The end of the line <paramref name="position"/> is on, before its trailing comment and without trailing whitespace.</summary>
    private static int LineEnd(SourceText source, Dictionary<int, int> commentStarts, int position)
    {
        int number = source.GetLinePosition(position).Line;
        TextSpan line = source.GetLineSpan(number);
        int end = commentStarts.TryGetValue(number, out int comment) ? comment : line.End;
        return line.Start + source.Text.AsSpan(line.Start, end - line.Start).TrimEnd(" \t").Length;
    }
}
