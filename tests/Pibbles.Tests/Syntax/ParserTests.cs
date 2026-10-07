using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class ParserTests
{
    [Theory]
    [InlineData("@wait 0.1", "0.1")]
    [InlineData("@wait 1.50", "1.50")]
    [InlineData("@wait 300ms", "0.3")]
    [InlineData("@wait 1.5s", "1.5")]
    [InlineData("@wait 79228162514264337593543950335", "79228162514264337593543950335")]
    [InlineData("@wait 1.12345678901234567890123456789", "1.1234567890123456789012345679")]
    public void Parse_NumberOrDuration_IsExactDecimalWithItsScale(string text, string expected)
    {
        var source = new SourceText("story.pib", $"== a.b\n{text}\n");

        var wait = (WaitStatementSyntax)SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        decimal value = wait.Duration switch
        {
            NumberLiteralSyntax number => number.Value,
            DurationLiteralSyntax duration => duration.Seconds,
            _ => throw new Xunit.Sdk.XunitException("Not a literal."),
        };
        Assert.Equal(expected, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("@wait 79228162514264337593543950336")]
    [InlineData("@wait 100000000000000000000000000000s")]
    public void Parse_NumberTooLarge_ReportsItAndMakesAnErrorNode(string text)
    {
        var source = new SourceText("story.pib", $"== a.b\n{text}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1047", Assert.Single(tree.Diagnostics).Code);
        Assert.IsType<ErrorExpressionSyntax>(((WaitStatementSyntax)tree.Root.Nodes[0].Body[0]).Duration);
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
