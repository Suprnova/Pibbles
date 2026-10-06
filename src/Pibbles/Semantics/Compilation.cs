using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>A whole story, analyzed: every file parsed, and every name in them checked against the story's declarations.</summary>
/// <remarks>
/// A story is every source file in it, compiled together, since names are global across files. Creating a compilation
/// never throws on bad input; every problem is in <see cref="Diagnostics"/>.
/// </remarks>
public sealed class Compilation
{
    private Compilation(IReadOnlyList<SyntaxTree> syntaxTrees, SymbolTable symbols, IReadOnlyList<Diagnostic> diagnostics)
    {
        SyntaxTrees = syntaxTrees;
        Symbols = symbols;
        Diagnostics = diagnostics;
    }

    /// <summary>The story's files, parsed, in the order they were given.</summary>
    public IReadOnlyList<SyntaxTree> SyntaxTrees { get; }

    /// <summary>
    /// Every problem in the story, from parsing and from analysis: grouped by file in the order the files were given,
    /// then in the order they appear in each file.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    internal SymbolTable Symbols { get; }

    /// <summary>Compiles a story.</summary>
    /// <param name="sources">Every source file in the story. Each path should be unique, since diagnostics refer to files by path.</param>
    public static Compilation Create(IEnumerable<SourceText> sources)
    {
        SyntaxTree[] trees = [.. sources.Select(SyntaxTree.Parse)];
        List<Diagnostic> found = [.. trees.SelectMany(tree => tree.Diagnostics)];
        SymbolTable symbols = DeclarationPass.Run(trees, found);
        Binder.Run(trees, symbols, found);

        Dictionary<string, int> fileOrder = trees.Select((tree, index) => (tree.Source.Path, index)).DistinctBy(file => file.Path).ToDictionary();
        return new(trees, symbols, [.. found.OrderBy(diagnostic => fileOrder[diagnostic.Location.Path]).ThenBy(diagnostic => diagnostic.Location.Span.Start)]);
    }
}
