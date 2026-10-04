using Pibbles.Syntax;
using static VerifyXunit.Verifier;

namespace Pibbles.Tests.Snapshots;

/// <summary>
/// Syntax snapshots: each <c>.pib</c> input's tree, compared with its <c>.verified.txt</c>. Inputs in <c>Recovery/</c>
/// have mistakes on purpose, and their snapshots also list the diagnostics, pinning down the tree a broken file leaves.
/// </summary>
public class SnapshotTests
{
    private static readonly string Directory = Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Snapshots");

    public static TheoryData<string> Inputs { get; } = [.. FindInputs().Select(path => Path.GetRelativePath(Directory, path))];

    [Theory]
    [MemberData(nameof(Inputs))]
    public Task Parse_SnapshotInput_MatchesVerifiedTree(string input)
    {
        string path = Path.Combine(Directory, input);
        SyntaxTree tree = Parse(path);

        return Verify(Describe(tree))
            .UseDirectory(Path.GetDirectoryName(path)!)
            .UseFileName(Path.GetFileNameWithoutExtension(path));
    }

    [Fact]
    public void Inputs_OutsideRecovery_ParseWithoutDiagnostics()
    {
        string[] noisy = [.. System.IO.Directory.EnumerateFiles(Directory, "*.pib")
            .Where(path => Parse(path).Diagnostics.Count > 0)
            .Select(path => Path.GetFileName(path)!)];

        Assert.Empty(noisy);
    }

    [Fact]
    public void Inputs_CoverEveryNodeKind()
    {
        HashSet<Type> seen = [];
        foreach (string path in FindInputs())
            SyntaxDump.Write(Parse(path), seen);

        string[] missing = [.. typeof(SyntaxNode).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SyntaxNode)) && !type.IsAbstract && !seen.Contains(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)];

        Assert.Empty(missing);
    }

    private static string Describe(SyntaxTree tree)
    {
        string dump = SyntaxDump.Write(tree);
        if (tree.Diagnostics.Count == 0)
            return dump;

        IEnumerable<string> diagnostics = tree.Diagnostics.Select(diagnostic =>
            $"  {diagnostic.Code} {diagnostic.Location.Start.Line + 1}:{diagnostic.Location.Start.Column + 1} {diagnostic.Message}\n");
        return dump + "Diagnostics:\n" + string.Concat(diagnostics);
    }

    private static IEnumerable<string> FindInputs() => System.IO.Directory.EnumerateFiles(Directory, "*.pib", SearchOption.AllDirectories);

    private static SyntaxTree Parse(string path) => SyntaxTree.Parse(new SourceText(Path.GetFileName(path), File.ReadAllText(path)));
}
