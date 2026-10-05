using Pibbles.Cli.Output;

namespace Pibbles.Tests.Cli;

public class MarkupTests
{
    [Theory]
    [InlineData("Remove it: `@if $x`.", "Remove it: `@if $x`.")]
    [InlineData("**Spans close first.** Then more.", "Spans close first. Then more.")]
    [InlineData("A lone ` backtick stays.", "A lone ` backtick stays.")]
    [InlineData("**Bold with `code` inside.**", "Bold with `code` inside.")]
    public void Render_WithoutColor_KeepsBackticksAndDropsAsterisks(string text, string expected)
    {
        string rendered = Markup.Render(text, color: false);

        Assert.Equal(expected, rendered);
    }

    [Theory]
    [InlineData("Remove it: `@if $x`.", "Remove it: \e[36m@if $x\e[39m.")]
    [InlineData("**Bold.** Plain.", "\e[1mBold.\e[22m Plain.")]
    public void Render_WithColor_PaintsCodeAndBold(string text, string expected)
    {
        string rendered = Markup.Render(text, color: true);

        Assert.Equal(expected, rendered);
    }
}
