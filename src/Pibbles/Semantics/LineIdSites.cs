using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>A line that needs a line ID: its <c>#id</c> tag if it has one, and where a new one goes.</summary>
/// <param name="Id">The line's <c>#id</c> tag, or <see langword="null"/> if it has none.</param>
/// <param name="Insert">Where a new ID goes: after the line's last tag, or its content if it has none, and before a trailing comment.</param>
internal readonly record struct LineIdSite(TagSyntax? Id, int Insert);

/// <summary>
/// Finds the lines that need a line ID: text lines that show text, options, <c>@call</c>, and variation block openers.
/// A pose change shows nothing, so a save never stops on it, and it takes no ID.
/// </summary>
internal static class LineIdSites
{
    public static IEnumerable<LineIdSite> In(SyntaxTree tree) => tree.Root.Nodes.SelectMany(node => In(tree.Source, node.Body));

    private static IEnumerable<LineIdSite> In(SourceText source, IEnumerable<StatementSyntax> statements) => statements.SelectMany(statement => statement switch
    {
        TextLineSyntax { Pose: not null, Content: [] } => [],
        TextLineSyntax line => [new(IdOf(line.Tags), line.Span.End)],
        CallStatementSyntax call => [new(IdOf(call.Tags), call.Span.End)],
        ChoiceSyntax choice => choice.Options.SelectMany(option => (IEnumerable<LineIdSite>)[new(IdOf(option.Tags), LineEnd(source, option.Span.Start)), .. In(source, option.Body)]),
        VariationStatementSyntax variation => [Opener(variation.Span, variation.Tags, variation.Kind is VariationKind.Sequence ? "@sequence" : "@cycle"), .. variation.Alternatives.SelectMany(alternative => In(source, alternative.Body))],
        OnceStatementSyntax once => [Opener(once.Span, once.Tags, "@once"), .. In(source, once.Body)],
        IfStatementSyntax @if => [.. In(source, @if.Body), .. @if.ElseIfs.SelectMany(elseIf => In(source, elseIf.Body)), .. In(source, @if.Else?.Body ?? [])],
        _ => [],
    });

    private static LineIdSite Opener(TextSpan span, IReadOnlyList<TagSyntax> tags, string keyword) =>
        new(IdOf(tags), tags.Count > 0 ? tags[^1].Span.End : span.Start + keyword.Length);

    private static TagSyntax? IdOf(IEnumerable<TagSyntax> tags) => tags.FirstOrDefault(tag => tag.Name is "id");

    /// <summary>The end of the line <paramref name="position"/> is on, without trailing whitespace.</summary>
    private static int LineEnd(SourceText source, int position)
    {
        TextSpan line = source.GetLineSpan(source.GetLinePosition(position).Line);
        return line.Start + source.Text.AsSpan(line.Start, line.Length).TrimEnd(" \t").Length;
    }
}
