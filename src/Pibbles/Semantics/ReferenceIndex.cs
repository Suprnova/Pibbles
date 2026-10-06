using Pibbles.Diagnostics;

namespace Pibbles.Semantics;

/// <summary>Every place a story names a symbol, where it's declared and where it's used, for the <see cref="SemanticModel"/>.</summary>
internal sealed class ReferenceIndex
{
    private readonly List<(SourceLocation Location, Symbol Symbol, bool IsDeclaration)> entries = [];

    public void Add(SourceLocation location, Symbol symbol, bool isDeclaration) => entries.Add((location, symbol, isDeclaration));

    /// <summary>The symbol named at a position, from the innermost name that touches it.</summary>
    public Symbol? SymbolAt(string path, int position) => entries
        .Where(entry => entry.Location.Path == path && entry.Location.Span.Start <= position && position <= entry.Location.Span.End)
        .OrderBy(entry => entry.Location.Span.Length)
        .Select(entry => entry.Symbol)
        .FirstOrDefault();

    /// <summary>Where a symbol is used, in the order the files were given, then by position.</summary>
    public IEnumerable<SourceLocation> UsesOf(Symbol symbol) => entries
        .Where(entry => !entry.IsDeclaration && entry.Symbol == symbol)
        .Select(entry => entry.Location);
}
