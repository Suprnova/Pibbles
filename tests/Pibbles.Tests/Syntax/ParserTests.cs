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
}
