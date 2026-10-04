using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class ParserTests
{
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
}
