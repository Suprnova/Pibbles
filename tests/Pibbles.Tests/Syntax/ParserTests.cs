using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class ParserTests
{
    [Theory]
    [InlineData("@pause 0.1", "0.1")]
    [InlineData("@pause 1.50", "1.50")]
    [InlineData("@pause 300ms", "0.3")]
    [InlineData("@pause 1.5s", "1.5")]
    [InlineData("@pause 79228162514264337593543950335", "79228162514264337593543950335")]
    [InlineData("@pause 1.12345678901234567890123456789", "1.1234567890123456789012345679")]
    public void Parse_NumberOrDuration_IsExactDecimalWithItsScale(string text, string expected)
    {
        var source = new SourceText("story.pib", $"== a.b\n{text}\n");

        var pause = (PauseStatementSyntax)SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        decimal value = pause.Duration switch
        {
            NumberLiteralSyntax number => number.Value,
            DurationLiteralSyntax duration => duration.Seconds,
            _ => throw new Xunit.Sdk.XunitException("Not a literal."),
        };
        Assert.Equal(expected, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_AtWait_IsAnOrdinaryCommand()
    {
        var source = new SourceText("story.pib", "== a.b\n@wait 1s\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal("wait", Assert.IsType<CommandStatementSyntax>(tree.Root.Nodes[0].Body[0]).Command.Text);
    }

    [Theory]
    [InlineData("@pause 79228162514264337593543950336")]
    [InlineData("@pause 100000000000000000000000000000s")]
    public void Parse_NumberTooLarge_ReportsItAndMakesAnErrorNode(string text)
    {
        var source = new SourceText("story.pib", $"== a.b\n{text}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1047", Assert.Single(tree.Diagnostics).Code);
        Assert.IsType<ErrorExpressionSyntax>(((PauseStatementSyntax)tree.Root.Nodes[0].Body[0]).Duration);
    }

    [Theory]
    [InlineData("@prefix kitchen\n@prefix cellar", 1)]
    [InlineData("// A comment.\n\n@prefix kitchen\n== .door\n@prefix cellar", 3)]
    public void Parse_SecondPrefix_NamesLineOfFirst(string text, int line)
    {
        var source = new SourceText("story.pib", text);

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1023", $"This file already has a `@prefix`, on line {line}."), (diagnostic.Code, diagnostic.Message));
    }

    [Theory]
    [InlineData("mira: I'm #winning today.", "what mira says", "mira: I'm \\#winning today.")]
    [InlineData("I'm #winning today.", "the text", "I'm \\#winning today.")]
    [InlineData("-> Go #show_disabled @once", "the option's text", "-> Go \\#show_disabled @once")]
    public void Parse_TextAfterTag_HelpNamesWhoseTextAndShowsFixedLine(string line, string owner, string fixedLine)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}  \n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        string tag = line[line.IndexOf('#', StringComparison.Ordinal)..].Split(' ')[0];
        Assert.Equal($"If `{tag}` is part of {owner}, put a backslash before the `#`:\n{fixedLine}", diagnostic.Help);
    }

    [Theory]
    [InlineData("mira: Hi{w}", typeof(InputWaitSyntax))]
    [InlineData("mira: Hi{pause 0.5}", typeof(PauseSyntax))]
    public void Parse_WaitOrPausePoint_MakesItsOwnNode(string line, Type expected)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        var text = (TextLineSyntax)SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        Assert.IsType(expected, text.Content[^1]);
    }

    [Theory]
    [InlineData("-> Knock{w}", "{w}")]
    [InlineData("-> Knock{pause 0.5}", "{pause}")]
    [InlineData("-> Knock{page}", "{page}")]
    public void Parse_WaitOrPauseInOption_NamesThePointWritten(string line, string point)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n    mira: Hi\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(($"An option's text can't contain `{point}`.", "PIB1016"), (diagnostic.Message, diagnostic.Code));
    }

    [Theory]
    [InlineData("w", "0.5", "To pause for that long, write `{pause 0.5}`.")]
    [InlineData("w", "$delay", "To pause for that long, write `{pause $delay}`.")]
    [InlineData("page", "2", "Remove `2`.")]
    [InlineData("br", "(1 + 1)", "Remove `(1 + 1)`.")]
    public void Parse_ValueAfterPointThatTakesNone_NamesThePointAndOffersAFix(string point, string value, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{{{point} {value}}} there\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1018", $"`{{{point}}}` takes no value, so I can't use `{value}` after it.", help), (diagnostic.Code, diagnostic.Message, diagnostic.Help));
    }

    [Theory]
    [InlineData("{p}", "To break the page, write `{page}`.")]
    [InlineData("{p 0.5}", "To break the page, write `{page}`. To pause for that long, write `{pause 0.5}`.")]
    public void Parse_OldPageBreak_NamesThePointAndOffersThePageBreak(string point, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{point} there\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Diagnostic diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(("PIB1017", $"I don't know what `{point}` means.", help), (diagnostic.Code, diagnostic.Message, diagnostic.Help));
    }

    [Fact]
    public void Parse_OldPageBreakInOption_IsReportedOnce()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n-> Knock{p}\n    mira: Hi\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal("PIB1017", diagnostic.Code);
    }

    [Theory]
    [InlineData("{speed 2 3}")]
    [InlineData("{pause 1 2}")]
    public void Parse_SecondValueAfterPointThatTakesOne_IsUnexpectedNotPointTakesNoValue(string point)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{point} there\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal("PIB1040", diagnostic.Code);
    }

    [Fact]
    public void Parse_PauseWithoutDuration_ReportsMissingValueAndKeepsThePoint()
    {
        var source = new SourceText("story.pib", "== kitchen.door\nmira: Hi{pause}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var pause = (PauseSyntax)((TextLineSyntax)tree.Root.Nodes[0].Body[0]).Content[^1];
        Assert.Equal(("PIB1046", true), (Assert.Single(tree.Diagnostics).Code, pause.Duration is ErrorExpressionSyntax));
    }

    [Theory]
    [InlineData("mira: [b]unclosed")]
    [InlineData("mira: {$unclosed")]
    [InlineData("-> [b]unclosed")]
    public void Parse_UnclosedAtEndOfLineWithTrailingWhitespace_StaysInsideTheLine(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}  \t\n");

        StatementSyntax statement = SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        Assert.All(NodeFields.DescendantsAndSelf(statement), node => Assert.True(node.Span.End <= statement.Span.End, $"{node.GetType().Name} at {node.Span} ends after its line."));
    }
}
