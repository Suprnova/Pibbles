using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Checks line IDs across the story: every line that needs one has one, and each is used once and never names a node.
/// A line with a syntax error isn't checked, since its tags may not have been read.
/// </summary>
internal sealed class LineIdChecks(SymbolTable symbols, List<Diagnostic> diagnostics) : AnalysisPass(diagnostics, new ReferenceIndex())
{
    private readonly Dictionary<string, SourceLocation> used = [];

    public static void Run(IReadOnlyList<SyntaxTree> trees, SymbolTable symbols, List<Diagnostic> diagnostics)
    {
        var checks = new LineIdChecks(symbols, diagnostics);
        foreach (SyntaxTree tree in trees)
        {
            checks.Tree = tree;
            checks.Check();
        }
    }

    private void Check()
    {
        HashSet<int> broken = [.. Tree.Diagnostics.Select(diagnostic => diagnostic.Location.Start.Line)];
        foreach (LineIdSite site in LineIdSites.In(Tree).Where(site => !broken.Contains(Tree.Source.GetLinePosition(site.Insert).Line)))
        {
            if (site.Id is not { Value: { } id } tag)
            {
                Report(DiagnosticCatalog.MissingLineId, new(site.Insert, 0));
            }
            else if (symbols.Nodes.ContainsKey(id))
            {
                Report(DiagnosticCatalog.LineIdIsNodeName, tag.Span, id, "the name of a node");
            }
            else if (symbols.Aliases.TryGetValue(id, out NodeSymbol? node))
            {
                Report(DiagnosticCatalog.LineIdIsNodeName, tag.Span, id, $"an old name of `{node.Name}`");
            }
            else if (used.TryGetValue(id, out SourceLocation first))
            {
                Report(DiagnosticCatalog.DuplicateLineId, tag.Span, id, Where(first));
            }
            else
            {
                used.Add(id, Tree.Source.GetLocation(tag.Span));
            }
        }
    }
}
