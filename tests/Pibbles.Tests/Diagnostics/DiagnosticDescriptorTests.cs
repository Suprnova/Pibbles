using System.Globalization;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Diagnostics;

public class DiagnosticDescriptorTests
{
    private readonly SourceLocation location = new SourceText("story.pib", "mira: Hi.").GetLocation(new(0, 4));

    [Fact]
    public void Create_WithAllTemplates_FormatsEachWithArguments()
    {
        var descriptor = new DiagnosticDescriptor("PIB1010", DiagnosticSeverity.Error, "I can't find the end of this `[{0}]`.", "opened here", "Close it with `[/{0}]`.");

        Diagnostic diagnostic = descriptor.Create(location, "clue");

        Assert.Equal(new("PIB1010", DiagnosticSeverity.Error, location, "I can't find the end of this `[clue]`.", "opened here", "Close it with `[/clue]`."), diagnostic);
    }

    [Fact]
    public void Create_WithoutLabelOrHelp_LeavesThemNull()
    {
        var descriptor = new DiagnosticDescriptor("PIB3001", DiagnosticSeverity.Warning, "This line never runs.");

        Diagnostic diagnostic = descriptor.Create(location);

        Assert.Equal((null, null), (diagnostic.Label, diagnostic.Help));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Create_UnderCultureWithDecimalComma_FormatsNumbersWithDot(string culture)
    {
        var descriptor = new DiagnosticDescriptor("PIB1042", DiagnosticSeverity.Error, "Write it as `{0}`.");
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            Diagnostic diagnostic = descriptor.Create(location, 0.5);

            Assert.Equal("Write it as `0.5`.", diagnostic.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
