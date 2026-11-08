using Pibbles.Diagnostics;

namespace Pibbles.Semantics;

/// <summary>Every place a story names a symbol, where it's declared and where it's used, for the <see cref="SemanticModel"/>.</summary>
internal sealed class ReferenceIndex
{
    private readonly List<(SourceLocation Location, Symbol Symbol, bool IsDeclaration)> entries = [];

    /// <summary>Records that the text at <paramref name="location"/> names <paramref name="symbol"/>.</summary>
    /// <param name="location">Where the name is written.</param>
    /// <param name="symbol">The symbol the name refers to.</param>
    /// <param name="isDeclaration"><see langword="true"/> if the name declares the symbol, <see langword="false"/> if it uses it.</param>
    public void Add(SourceLocation location, Symbol symbol, bool isDeclaration) => entries.Add((location, symbol, isDeclaration));

    /// <summary>Finds the symbol named at a position, from the innermost name that touches it.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="position">A position in the file.</param>
    /// <returns>The symbol, or <see langword="null"/> if no name touches the position.</returns>
    public Symbol? SymbolAt(string path, int position) => entries
        .Where(entry => entry.Location.Path == path && entry.Location.Span.Start <= position && position <= entry.Location.Span.End)
        .OrderBy(entry => entry.Location.Span.Length)
        .Select(entry => entry.Symbol)
        .FirstOrDefault();

    /// <summary>Finds where a symbol is used, not counting its declaration.</summary>
    /// <param name="symbol">The symbol to look for.</param>
    /// <returns>The uses, grouped by file in the order the files were given, then in order of position.</returns>
    public IEnumerable<SourceLocation> UsesOf(Symbol symbol) => entries
        .Where(entry => !entry.IsDeclaration && entry.Symbol == symbol)
        .Select(entry => entry.Location)
        .GroupBy(location => location.Path)
        .SelectMany(file => file.OrderBy(location => location.Span.Start));
}
