using Pibbles.Diagnostics;

namespace Pibbles.Semantics;

/// <summary>
/// What a compiled story means: every symbol it declares, and where each name in it refers to one. It answers the
/// questions an editor asks, such as which symbol is under the cursor and where else it's used.
/// </summary>
/// <remarks>A symbol's declaration is its <see cref="Symbol.Location"/>.</remarks>
public sealed class SemanticModel
{
    private readonly SymbolTable symbols;
    private readonly ReferenceIndex references;

    internal SemanticModel(SymbolTable symbols, ReferenceIndex references)
    {
        this.symbols = symbols;
        this.references = references;
    }

    /// <summary>Finds the symbol a name refers to, where it's used or where it's declared.</summary>
    /// <param name="path">The file's path, as given in its <see cref="Syntax.SourceText"/>.</param>
    /// <param name="position">A position in the file. A position just past the end of a name still counts as on it.</param>
    /// <returns>The symbol, or <see langword="null"/> if no name that refers to one is there.</returns>
    public Symbol? GetSymbolAt(string path, int position) => references.SymbolAt(path, position);

    /// <summary>Finds every place the story uses a symbol, not counting where it's declared.</summary>
    /// <param name="symbol">A symbol from this model.</param>
    /// <returns>Where the symbol is used, grouped by file in the order the files were given.</returns>
    public IReadOnlyList<SourceLocation> FindReferences(Symbol symbol) => [.. references.UsesOf(symbol)];

    /// <summary>
    /// Lists the symbols of one kind, such as every <see cref="ActorSymbol"/>, including the ones built into Pibbles.
    /// Poses, enum members and parameters are listed along with the symbols they belong to.
    /// </summary>
    /// <typeparam name="T">The kind of symbol, or <see cref="Symbol"/> for all of them.</typeparam>
    public IEnumerable<T> Symbols<T>()
        where T : Symbol => symbols.All().OfType<T>();
}
