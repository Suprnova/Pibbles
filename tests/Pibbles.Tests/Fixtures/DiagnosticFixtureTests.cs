using Pibbles.Diagnostics;
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

        string annotated = FixtureFile.Annotate(source, Diagnose(source));

        Assert.Equal(text.ReplaceLineEndings("\n"), annotated);
    }

    /// <summary>Classifies the lines, then lexes each header and <c>@</c> line in code mode. The parser replaces this once it exists.</summary>
    private static List<Diagnostic> Diagnose(SourceText source)
    {
        ClassifiedLines classified = LineClassifier.Classify(source);
        List<Diagnostic> diagnostics = [.. classified.Diagnostics];

        foreach (Line line in classified.Tokens.Where(token => token.Kind is LineTokenKind.Line).Select(token => token.Line))
        {
            if (line.Kind is not (LineKind.Header or LineKind.At))
                continue;

            int marker = line.Kind is LineKind.Header ? 2 : 0;
            var lexer = new CodeLexer(source, new(line.Content.Start + marker, line.Content.Length - marker), diagnostics);
            while (lexer.Next().Kind is not TokenKind.EndOfLine)
            {
            }
        }

        return diagnostics;
    }
}
