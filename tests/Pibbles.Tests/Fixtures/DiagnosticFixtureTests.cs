using Pibbles.Diagnostics;
using Pibbles.Semantics;
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

        string annotated = FixtureFile.Annotate(source, Diagnose(fixture, source));

        Assert.Equal(text.ReplaceLineEndings("\n"), annotated);
    }

    /// <summary>
    /// Syntax fixtures are only parsed, since they use names they don't declare. Every other fixture compiles as a story
    /// of one file. Fixtures leave out line IDs, and each keeps to its own topic, so a missing ID or a style rule is only
    /// reported in its own fixture.
    /// </summary>
    private static IReadOnlyList<Diagnostic> Diagnose(string fixture, SourceText source) =>
        Path.GetDirectoryName(fixture) is "Syntax"
            ? SyntaxTree.Parse(source).Diagnostics
            : [.. Compilation.Create([source]).Diagnostics.Where(diagnostic => !IsOwnTopic(diagnostic.Code) || Path.GetFileNameWithoutExtension(fixture) == diagnostic.Code)];

    private static bool IsOwnTopic(string code) => code is "PIB3010" or ['P', 'I', 'B', '5', ..];
}
