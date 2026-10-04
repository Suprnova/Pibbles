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
            "PIB1010 (error): I can't find the end of this `[clue]`.\nHelp: Close it with `[/clue]`.\n\n**A span closes on its line.**\n\n    mira: [clue]Key\n\nClose it.\n\n",
            output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_CodeWithoutExplanation_PrintsHeadlineAndHelp()
    {
        Explain.Run("PIB2001", Catalog, output, error);

        Assert.Equal("PIB2001 (error): I don't know an actor called `Note`.\nHelp: Escape the colon.\n", output.ToString().ReplaceLineEndings("\n"));
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
            Assert.Contains("\n**", explanation.ToString().ReplaceLineEndings("\n"));
        });
    }
}
