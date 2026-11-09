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

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code is "PIB3204" && diagnostic.Help!.Contains("`{if …}…{/if}`", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("@enum position:\n@var $p: position = left\n")]
    [InlineData("@enum position:\n@var $p: position = 1\n")]
    public void Compile_ValueOfEnumWithNoMembers_IsNotReported(string story)
    {
        var compilation = Compilation.Create([new SourceText("story.pib", story)]);

        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Code is "PIB3005" or "PIB2303");
    }

    [Theory]
    [InlineData("@var $t = not\n")]
    [InlineData("@var $u = and\n")]
    [InlineData("@var $w: bool = not true\n")]
    [InlineData("@command wave(times: number = or)\n")]
    public void Compile_OperatorWordAsConstant_ReportsOnlyTheWord(string story)
    {
        var compilation = Compilation.Create([new SourceText("story.pib", story)]);

        Assert.Equal(["PIB1103"], compilation.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void Compile_EnumWithNoMembersAsATagType_SuggestsAGenericValue()
    {
        var compilation = Compilation.Create([new SourceText("story.pib", "@enum position:\n@tag box: position\n\n== a.b\nHi. #box\n")]);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code is "PIB1206" && diagnostic.Help!.Contains("#box:value", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_DeeplyNestedBlockWithNoBody_DoesNotThrow()
    {
        string story = "@var $a = true\n\n== a.b\n" + string.Concat(Enumerable.Range(0, 12).Select(depth => $"{new string(' ', depth * 4)}@if $a\n"));

        var compilation = Compilation.Create([new SourceText("story.pib", story)]);

        Assert.NotEmpty(compilation.Diagnostics);
    }
}
