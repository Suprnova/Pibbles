using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

/// <summary>Bugs the property tests found in analysis of broken input, kept as examples so the fixed sequence needn't find them again.</summary>
public class RecoveryTests
{
    [Fact]
    public void Compile_EnumWithNoMembersShownInText_ReportsWithoutAnExample()
    {
        var compilation = Compilation.Create([new SourceText("story.pib", "@enum position:\n@var $p: position = left\n\n== a.b\nYou're on the {$p}.\n")]);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code is "PIB2038");
    }

    [Fact]
    public void Compile_EnumWithNoMembersAsATagType_SuggestsAGenericValue()
    {
        var compilation = Compilation.Create([new SourceText("story.pib", "@enum position:\n@tag box: position\n\n== a.b\nHi. #box\n")]);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code is "PIB2046" && diagnostic.Help!.Contains("#box:value", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_DeeplyNestedBlockWithNoBody_DoesNotThrow()
    {
        string story = "@var $a = true\n\n== a.b\n" + string.Concat(Enumerable.Range(0, 12).Select(depth => $"{new string(' ', depth * 4)}@if $a\n"));

        var compilation = Compilation.Create([new SourceText("story.pib", story)]);

        Assert.NotEmpty(compilation.Diagnostics);
    }
}
