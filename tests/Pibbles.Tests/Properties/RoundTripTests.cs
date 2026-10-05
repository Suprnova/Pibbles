using CsCheck;
using Pibbles.Syntax;
using Pibbles.Tests.Snapshots;

namespace Pibbles.Tests.Properties;

public class RoundTripTests
{
    [Fact]
    public void Parse_PrintedTree_GivesBackTheTree() => PropertyCheck.Run(SyntaxGenerators.File, file =>
    {
        var source = new SourceText("printed.pib", SyntaxPrinter.Print(file));

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(SyntaxDump.WriteShape(file), SyntaxDump.WriteShape(tree.Root));
    }, iterations: 500, print: SyntaxPrinter.Print);

    [Fact]
    public void Generator_CoversEveryNodeKindButErrors()
    {
        var pcg = new PCG(1, 1);
        HashSet<Type> seen = [];

        for (int i = 0; i < 200; i++)
            SyntaxDump.WriteShape(SyntaxGenerators.File.Generate(pcg, null, out _), seen);

        string[] missing = [.. typeof(SyntaxNode).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SyntaxNode)) && !type.IsAbstract && type != typeof(ErrorExpressionSyntax) && !seen.Contains(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)];
        Assert.Empty(missing);
    }
}
