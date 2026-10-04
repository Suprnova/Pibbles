using Pibbles.Syntax;

namespace Pibbles.Tests.Samples;

public class KitchenTests
{
    public static TheoryData<string> Files { get; } =
        [.. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "samples", "kitchen"), "*.pib", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryRoot.Path, path))];

    [Theory]
    [MemberData(nameof(Files))]
    public void Parse_KitchenFile_ReportsNothing(string file)
    {
        var source = new SourceText(file, File.ReadAllText(Path.Combine(RepositoryRoot.Path, file)));

        Assert.Empty(SyntaxTree.Parse(source).Diagnostics);
    }
}
