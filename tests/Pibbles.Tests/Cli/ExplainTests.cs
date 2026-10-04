using Pibbles.Cli;
using Pibbles.Diagnostics;

namespace Pibbles.Tests.Cli;

public sealed class ExplainTests : IDisposable
{
    private const string Catalog = """
        ## Codes

        | Code | Default | Message | Help |
        | --- | --- | --- | --- |
        | PIB1010 | Error | I can't find the end of this `[clue]`. | Close it with `[/clue]`. |
        | PIB2001 | Error | I don't know an actor called `Note`. | Escape the colon. |

        ## Explanations

        ### PIB1010

        **A span closes on its line.**

        ```text
        mira: [clue]Key
        ```

        Close it.

        ## With extensions
        """;

    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
    }

    [Theory]
    [InlineData("PIB1010")]
    [InlineData("pib1010")]
    [InlineData(" PIB1010 ")]
    public void Run_DocumentedCode_PrintsHeadlineHelpAndExplanation(string code)
    {
        int exitCode = Explain.Run(code, Catalog, output, error);

        Assert.Equal(Check.Passed, exitCode);
        Assert.Equal(
            "error[PIB1010]: I can't find the end of this `[clue]`.\n  = help: Close it with `[/clue]`.\n\nA span closes on its line.\n\n    mira: [clue]Key\n\nClose it.\n\n",
            output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_CodeWithoutExplanation_PrintsHeadlineAndHelp()
    {
        Explain.Run("PIB2001", Catalog, output, error);

        Assert.Equal("error[PIB2001]: I don't know an actor called `Note`.\n  = help: Escape the colon.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("PIB9999")]
    [InlineData("hello")]
    public void Run_UnknownCode_ReportsItAndCouldNotRun(string code)
    {
        int exitCode = Explain.Run(code, Catalog, output, error);

        Assert.Equal((Check.CouldNotRun, ""), (exitCode, output.ToString()));
        Assert.StartsWith($"I don't know the code `{code}`.", error.ToString());
    }

    [Fact]
    public void Run_EmbeddedCatalog_ExplainsEveryRegisteredCode()
    {
        Assert.All(DiagnosticCatalog.All, descriptor =>
        {
            using var explanation = new StringWriter();

            Assert.Equal(Check.Passed, Explain.Run(descriptor.Code, explanation, error));
            Assert.Contains("\n\n", explanation.ToString().ReplaceLineEndings("\n"));
            Assert.DoesNotContain("**", explanation.ToString());
        });
    }

    [Fact]
    public void Run_WithColor_PaintsHeadlineAndExamples()
    {
        Explain.Run("PIB1010", Catalog, output, error, color: true);

        string text = output.ToString();
        Assert.Contains("\e[1;31merror[PIB1010]", text);
        Assert.Contains("    \e[32mmira: [clue]Key", text);
    }
}
