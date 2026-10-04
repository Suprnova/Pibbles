using System.Reflection;
using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Syntax;
using Pibbles.Tests.Fixtures;

namespace Pibbles.Tests.Diagnostics;

public partial class DiagnosticCatalogTests
{
    private readonly string documentation = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "diagnostics.md"));

    [Fact]
    public void All_ListsEveryDescriptorInCatalog()
    {
        DiagnosticDescriptor[] declared = [.. typeof(DiagnosticCatalog)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(DiagnosticDescriptor))
            .Select(property => (DiagnosticDescriptor)property.GetValue(null)!)];

        Assert.All(declared, descriptor => Assert.Contains(DiagnosticCatalog.All, registered => ReferenceEquals(registered, descriptor)));
    }

    [Fact]
    public void All_HasUniqueWellFormedCodes()
    {
        string[] codes = [.. DiagnosticCatalog.All.Select(descriptor => descriptor.Code)];

        Assert.All(codes, code => Assert.Matches(CodePattern(), code));
        Assert.Equal(codes.Distinct(), codes);
    }

    [Fact]
    public void All_EveryTemplateFormats()
    {
        SourceLocation location = new SourceText("story.pib", "").GetLocation(new(0, 0));

        Assert.All(DiagnosticCatalog.All, descriptor => descriptor.Create(location, "first", "second", "third"));
    }

    [Fact]
    public void All_EveryCodeIsInDocumentedTableWithItsSeverity()
    {
        Dictionary<string, string> table = Section("Codes")
            .Select(line => TableRow().Match(line))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups["code"].Value, match => match.Groups["severity"].Value);

        Assert.All(DiagnosticCatalog.All, descriptor =>
        {
            Assert.True(table.TryGetValue(descriptor.Code, out string? severity), $"{descriptor.Code} isn't in the table in docs/diagnostics.md.");
            Assert.Equal(descriptor.DefaultSeverity.ToString(), severity);
        });
    }

    [Fact]
    public void DocumentedSyntaxCodes_AreAllRegistered()
    {
        string[] documented = [.. Section("Codes").Select(line => TableRow().Match(line)).Where(match => match.Success && match.Groups["code"].Value.StartsWith("PIB1", StringComparison.Ordinal)).Select(match => match.Groups["code"].Value)];

        Assert.All(documented, code => Assert.Contains(DiagnosticCatalog.All, descriptor => descriptor.Code == code));
    }

    [Fact]
    public void All_EveryCodeHasDocumentedExplanation()
    {
        string[] explained = [.. Section("Explanations").Select(line => ExplanationHeading().Match(line)).Where(match => match.Success).Select(match => match.Groups["code"].Value)];

        Assert.All(DiagnosticCatalog.All, descriptor => Assert.Contains(descriptor.Code, explained));
    }

    [Fact]
    public void All_EveryCodeHasFixtureThatMarksIt()
    {
        Dictionary<string, string> fixtures = FixtureFile.FindAll().ToDictionary(path => Path.GetFileNameWithoutExtension(path), File.ReadAllText);

        Assert.All(DiagnosticCatalog.All, descriptor =>
        {
            Assert.True(fixtures.TryGetValue(descriptor.Code, out string? text), $"{descriptor.Code} has no fixture named {descriptor.Code}.pib.");
            Assert.Contains(descriptor.Code, FixtureFile.MarkedCodes(text));
        });
    }

    private IEnumerable<string> Section(string heading) => documentation
        .ReplaceLineEndings("\n")
        .Split('\n')
        .SkipWhile(line => line != $"## {heading}")
        .Skip(1)
        .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal));

    [GeneratedRegex(@"^PIB\d{4}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^\| (?<code>PIB\d{4}) \| (?<severity>\w+) \|")]
    private static partial Regex TableRow();

    [GeneratedRegex(@"^### (?<code>PIB\d{4})\b")]
    private static partial Regex ExplanationHeading();
}
