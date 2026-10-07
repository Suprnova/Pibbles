using Pibbles.Configuration;
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
    private Compilation(IReadOnlyList<SyntaxTree> syntaxTrees, SymbolTable symbols, SemanticModel model, IReadOnlyList<Diagnostic> diagnostics)
    {
        SyntaxTrees = syntaxTrees;
        Symbols = symbols;
        Model = model;
        Diagnostics = diagnostics;
    }

    /// <summary>The story's files, parsed, in the order they were given.</summary>
    public IReadOnlyList<SyntaxTree> SyntaxTrees { get; }

    /// <summary>
    /// Every problem in the story, from parsing and from analysis: grouped by file in the order the files were given,
    /// then in the order they appear in each file.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>What the story means: its symbols, and which symbol each name refers to.</summary>
    public SemanticModel Model { get; }

    internal SymbolTable Symbols { get; }

    /// <summary>Compiles a story.</summary>
    /// <param name="sources">Every source file in the story. Each path should be unique, since diagnostics refer to files by path.</param>
    /// <param name="options">How to compile it, such as each file's diagnostic severities, or <see langword="null"/> for the defaults.</param>
    public static Compilation Create(IEnumerable<SourceText> sources, CompilationOptions? options = null)
    {
        options ??= CompilationOptions.Default;
        SyntaxTree[] trees = [.. sources.Select(SyntaxTree.Parse)];
        List<Diagnostic> found = [.. trees.SelectMany(tree => tree.Diagnostics)];
        var references = new ReferenceIndex();
        SymbolTable symbols = DeclarationPass.Run(trees, found, references);
        Binder.Run(trees, symbols, found, references);
        FlowChecks.Run(trees, found);
        LineIdChecks.Run(trees, symbols, found);
        StyleChecks.Run(trees, symbols, references, options, found);

        Dictionary<string, int> fileOrder = trees.Select((tree, index) => (tree.Source.Path, index)).DistinctBy(file => file.Path).ToDictionary();
        Dictionary<string, Suppressions> suppressions = trees.DistinctBy(tree => tree.Source.Path).ToDictionary(tree => tree.Source.Path, Suppressions.Of);
        Diagnostic[] configured =
        [
            .. found
                .Select(diagnostic => diagnostic.NameSpeakers(speaker => symbols.Actors.GetValueOrDefault(speaker)?.DisplayName))
                .Select(diagnostic => options.Settings.GetValueOrDefault(diagnostic.Location.Path, FileSettings.None).Configure(diagnostic))
                .OfType<Diagnostic>()
                .Where(diagnostic => !suppressions[diagnostic.Location.Path].Silences(diagnostic)),
        ];

        HashSet<(string Path, int Line)> broken = [.. configured.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error && !IsStyle(diagnostic)).Select(LineOf)];
        return new(trees, symbols, new SemanticModel(symbols, references),
        [
            .. configured
                .Where(diagnostic => !IsStyle(diagnostic) || !broken.Contains(LineOf(diagnostic)))
                .OrderBy(diagnostic => fileOrder[diagnostic.Location.Path])
                .ThenBy(diagnostic => diagnostic.Location.Span.Start),
        ]);
    }

    /// <summary>Whether a diagnostic is a style rule's. A line with an error gets none, since the error comes first.</summary>
    private static bool IsStyle(Diagnostic diagnostic) => diagnostic.Code is ['P', 'I', 'B', '5', ..];

    private static (string Path, int Line) LineOf(Diagnostic diagnostic) => (diagnostic.Location.Path, diagnostic.Location.Start.Line);
}
