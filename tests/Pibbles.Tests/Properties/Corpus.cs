using Pibbles.Syntax;
using Pibbles.Tests.Documentation;

namespace Pibbles.Tests.Properties;

/// <summary>Real Pibbles source to mutate: the sample story, the starter story, the fixtures, the snapshot inputs and the documentation.</summary>
internal static class Corpus
{
    private static readonly string[] Folders = ["samples", "src/Pibbles.Cli/Projects", "tests/Pibbles.Tests/Fixtures", "tests/Pibbles.Tests/Snapshots", "docs/language"];

    public static IReadOnlyList<SourceText> Files { get; } =
    [
        .. Folders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, folder), "*.pib", SearchOption.AllDirectories))
            .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path), File.ReadAllText(path))),
        .. CodeBlock.All.Where(block => block.Info.StartsWith("pib", StringComparison.Ordinal)).Select(block => new SourceText(block.Name, block.Text)),
    ];

    /// <summary>The files that parse with no diagnostics.</summary>
    public static IReadOnlyList<SourceText> CleanFiles { get; } = [.. Files.Where(file => SyntaxTree.Parse(file).Diagnostics.Count == 0)];
}
