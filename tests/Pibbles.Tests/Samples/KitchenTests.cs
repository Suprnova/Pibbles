using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Samples;

public class KitchenTests
{
    [Fact]
    public void Compile_KitchenStory_ReportsNothing()
    {
        SourceText[] sources =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "samples", "kitchen"), "*.pib", SearchOption.AllDirectories)
                .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path), File.ReadAllText(path))),
        ];

        Assert.Empty(Compilation.Create(sources).Diagnostics);
    }
}
