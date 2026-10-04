using Pibbles.Syntax;

namespace Pibbles.Tests.Fixtures;

public class DiagnosticFixtureTests
{
    public static TheoryData<string> Fixtures { get; } = [.. FixtureFile.FindAll().Select(path => Path.GetRelativePath(FixtureFile.Directory, path))];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_ProducesExactlyMarkedDiagnostics(string fixture)
    {
        string text = File.ReadAllText(Path.Combine(FixtureFile.Directory, fixture));
        var source = new SourceText(fixture, text);

        string annotated = FixtureFile.Annotate(source, SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(text.ReplaceLineEndings("\n"), annotated);
    }
}
