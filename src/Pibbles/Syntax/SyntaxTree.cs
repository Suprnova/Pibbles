using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>One source file, parsed: its syntax tree and the problems found in its syntax.</summary>
/// <remarks>
/// A story is many files. Each one parses on its own, and the files only meet when their names are bound together.
/// </remarks>
public sealed class SyntaxTree
{
    internal SyntaxTree(SourceText source, FileSyntax root, IReadOnlyList<Diagnostic> diagnostics)
    {
        Source = source;
        Root = root;
        Diagnostics = diagnostics;
    }

    /// <summary>The parsed file.</summary>
    public SourceText Source { get; }

    /// <summary>The root of the tree.</summary>
    public FileSyntax Root { get; }

    /// <summary>The syntax problems in the file. Parsing never throws on bad input; every problem is reported here.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>Parses one source file.</summary>
    /// <param name="source">The file to parse.</param>
    public static SyntaxTree Parse(SourceText source) => Parser.Parse(source);
}
