using Pibbles.Syntax;
using static VerifyXunit.Verifier;

namespace Pibbles.Tests.Snapshots;

public class SnapshotTests
{
    private static readonly string Directory = Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Snapshots");

    public static TheoryData<string> Inputs { get; } = [.. FindInputs().Select(path => Path.GetFileNameWithoutExtension(path))];

    [Theory]
    [MemberData(nameof(Inputs))]
    public Task Parse_SnapshotInput_MatchesVerifiedTree(string name)
    {
        SyntaxTree tree = Parse(Path.Combine(Directory, $"{name}.pib"));

        return Verify(SyntaxDump.Write(tree))
            .UseDirectory(Directory)
            .UseFileName(name);
    }

    [Fact]
    public void Inputs_ParseWithoutDiagnostics()
    {
        string[] noisy = [.. FindInputs().Where(path => Parse(path).Diagnostics.Count > 0).Select(path => Path.GetFileName(path)!)];

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

    private static IEnumerable<string> FindInputs() => System.IO.Directory.EnumerateFiles(Directory, "*.pib");

    private static SyntaxTree Parse(string path) => SyntaxTree.Parse(new SourceText(Path.GetFileName(path), File.ReadAllText(path)));
}
