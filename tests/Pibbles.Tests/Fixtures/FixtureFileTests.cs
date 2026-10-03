using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Fixtures;

public class FixtureFileTests
{
    [Fact]
    public void Annotate_WithoutDiagnostics_RemovesMarkers()
    {
        var source = new SourceText("PIB1015.pib", "mira: I'm #winning today.\n//        ^^^^^^^^ PIB1015\n// PIB1001\nrex: Hm.");

        string annotated = FixtureFile.Annotate(source, []);

        Assert.Equal("mira: I'm #winning today.\nrex: Hm.", annotated);
    }

    [Fact]
    public void Annotate_SpanFromThirdColumn_AlignsCaretsUnderSpan()
    {
        var source = new SourceText("PIB1015.pib", "mira: I'm #winning today.");

        string annotated = FixtureFile.Annotate(source, [At(source, "PIB1015", 10, 8)]);

        Assert.Equal("mira: I'm #winning today.\n//        ^^^^^^^^ PIB1015", annotated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Annotate_SpanInFirstTwoColumns_MarksLineOnly(int column)
    {
        var source = new SourceText("PIB1005.pib", "/// A note.");

        string annotated = FixtureFile.Annotate(source, [At(source, "PIB1005", column, 3)]);

        Assert.Equal("/// A note.\n// PIB1005", annotated);
    }

    [Fact]
    public void Annotate_ZeroLengthSpan_MarksOneCaret()
    {
        var source = new SourceText("PIB1033.pib", "@if $door_open");

        string annotated = FixtureFile.Annotate(source, [At(source, "PIB1033", 14, 0)]);

        Assert.Equal("@if $door_open\n//            ^ PIB1033", annotated);
    }

    [Fact]
    public void Annotate_SpanPastEndOfLine_StopsCaretsAtLineEnd()
    {
        var source = new SourceText("PIB1010.pib", "mira: [clue]Key\nrex: Hm.");

        string annotated = FixtureFile.Annotate(source, [At(source, "PIB1010", 6, 12)]);

        Assert.Equal("mira: [clue]Key\n//    ^^^^^^^^^ PIB1010\nrex: Hm.", annotated);
    }

    [Fact]
    public void Annotate_SeveralOnOneLine_OrdersByColumnThenCode()
    {
        var source = new SourceText("PIB1011.pib", "mira: [b][i]Hi[/b]");

        string annotated = FixtureFile.Annotate(source, [At(source, "PIB1011", 14, 4), At(source, "PIB1012", 6, 3), At(source, "PIB1010", 6, 3)]);

        Assert.Equal("mira: [b][i]Hi[/b]\n//    ^^^ PIB1010\n//    ^^^ PIB1012\n//            ^^^^ PIB1011", annotated);
    }

    [Fact]
    public void Annotate_CrLfFixture_NormalizesLineBreaks()
    {
        var source = new SourceText("PIB1001.pib", "a\r\nb\r\n");

        string annotated = FixtureFile.Annotate(source, []);

        Assert.Equal("a\nb\n", annotated);
    }

    [Fact]
    public void MarkedCodes_ReadsEveryMarker()
    {
        string text = "mira: I'm #winning today.\n//        ^^^^^^^^ PIB1015\n/// A note.\n// PIB1005\n// Must not trigger\n// PIB1001 is about tabs.";

        Assert.Equal(["PIB1015", "PIB1005"], FixtureFile.MarkedCodes(text));
    }

    private static Diagnostic At(SourceText source, string code, int start, int length) =>
        new(code, DiagnosticSeverity.Error, source.GetLocation(new(start, length)), "message", null, null);
}
