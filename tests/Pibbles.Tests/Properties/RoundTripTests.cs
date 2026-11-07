using CsCheck;
using Pibbles.Syntax;
using Pibbles.Tests.Snapshots;

namespace Pibbles.Tests.Properties;

public class RoundTripTests
{
    [Fact]
    public void Parse_PrintedTreeWithComments_GivesBackTheTreeAndComments() => PropertyCheck.Run(Gen.Select(SyntaxGenerators.File, SyntaxGenerators.Comments), input =>
    {
        (FileSyntax file, string[] comments) = input;
        var source = new SourceText("printed.pib", SyntaxPrinter.Print(file, comments));

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(SyntaxDump.WriteShape(file), SyntaxDump.WriteShape(tree.Root));
        Assert.Equal(comments, tree.Comments.Select(comment => comment.Text));
    }, iterations: 500, print: input => SyntaxPrinter.Print(input.Item1, input.Item2));

    [Fact]
    public void Parse_PrintedTreeWithComments_MarksCommentsAloneOnTheirLine() => PropertyCheck.Run(Gen.Select(SyntaxGenerators.File, SyntaxGenerators.Comments), input =>
    {
        (FileSyntax file, string[] comments) = input;
        var source = new SourceText("printed.pib", SyntaxPrinter.Print(file, comments));

        SyntaxTree tree = SyntaxTree.Parse(source);

        foreach (Comment comment in tree.Comments)
        {
            TextSpan line = source.GetLineSpan(source.GetLinePosition(comment.Span.Start).Line);
            bool aloneOnLine = source.Text.AsSpan(line.Start, comment.Span.Start - line.Start).Trim(" \t").IsEmpty;
            Assert.Equal(aloneOnLine, comment.OnOwnLine);
        }
    }, iterations: 500, print: input => SyntaxPrinter.Print(input.Item1, input.Item2));

    [Fact]
    public void Parse_PrintedCorpusFile_GivesBackTheTreeAndComments() => Assert.All(Corpus.CleanFiles, AssertRoundTrips);

    [Fact]
    public void Parse_PrintedMutatedFileThatParsesCleanly_GivesBackTheTreeAndComments() => PropertyCheck.Run(Mutations.MutatedFile, file =>
    {
        var source = new SourceText(file.Name, file.Text);
        if (SyntaxTree.Parse(source).Diagnostics.Count == 0)
            AssertRoundTrips(source);
    }, iterations: 3000, print: file => $"{file.Name}, mutated:\n{file.Text}");

    private static void AssertRoundTrips(SourceText source)
    {
        SyntaxTree tree = SyntaxTree.Parse(source);
        string[] comments = [.. tree.Comments.Select(comment => comment.Text)];

        SyntaxTree printed = SyntaxTree.Parse(new SourceText(source.Path, SyntaxPrinter.Print(tree.Root, comments)));

        Assert.Empty(printed.Diagnostics);
        Assert.Equal(SyntaxDump.WriteShape(tree.Root), SyntaxDump.WriteShape(printed.Root));
        Assert.Equal(comments, printed.Comments.Select(comment => comment.Text));
    }

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
