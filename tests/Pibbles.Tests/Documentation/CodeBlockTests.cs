using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Documentation;

public partial class CodeBlockTests
{
    private const string FragmentHeader = "== doc.example\n";

    private static readonly Dictionary<string, CodeBlock> Blocks = CodeBlock.All.ToDictionary(block => block.Name);

    public static TheoryData<string> LanguageDocuments { get; } = [.. CodeBlock.Documents];

    public static TheoryData<string> PibblesBlocks { get; } = [.. CodeBlock.All.Where(block => block.Info.StartsWith("pib", StringComparison.Ordinal) && HasValidInfo(block)).Select(block => block.Name)];

    [Theory]
    [MemberData(nameof(LanguageDocuments))]
    public void CodeBlocks_InLanguageDocument_HaveValidInfoStrings(string document)
    {
        string[] invalid = [.. CodeBlock.All.Where(block => block.Document == document && !HasValidInfo(block)).Select(block => $"line {block.Line}: `{block.Info}`")];

        Assert.Empty(invalid);
    }

    [Theory]
    [MemberData(nameof(PibblesBlocks))]
    public void Parse_CodeBlock_ReportsSyntaxCodesInItsInfoString(string name)
    {
        var block = Blocks[name];
        string[] expected = [.. block.Info.Split(' ').Skip(1).Where(code => code.StartsWith("PIB1", StringComparison.Ordinal))];

        var (diagnostics, headerLines) = Parse(block);

        Assert.True(diagnostics.Select(diagnostic => diagnostic.Code).SequenceEqual(expected),
            $"Expected [{string.Join(", ", expected)}], got:\n{string.Join("\n", diagnostics.Select(diagnostic => Describe(block, headerLines, diagnostic)))}");
    }

    [Fact]
    public void Parse_DocumentationPrelude_ReportsNothing()
    {
        var source = new SourceText("examples.pib", File.ReadAllText(Path.Combine(CodeBlock.Folder, "examples.pib")));

        Assert.Empty(SyntaxTree.Parse(source).Diagnostics);
    }

    private static bool HasValidInfo(CodeBlock block) => block.Info.Split(' ') switch
    {
        ["pib" or "pib-standalone"] => true,
        ["pib-error", .. var codes] => codes.Length > 0 && codes.All(code => Code().IsMatch(code)),
        _ => block.Info.Length > 0 && !block.Info.StartsWith("pib", StringComparison.Ordinal),
    };

    /// <summary>Parses a block, first wrapping it in a node if it's a fragment: a block with no prefix, declaration or node.</summary>
    private static (IReadOnlyList<Diagnostic> Diagnostics, int HeaderLines) Parse(CodeBlock block)
    {
        var tree = SyntaxTree.Parse(new SourceText(block.Name, block.Text));

        return tree.Root is { Prefix: null, Declarations: [], Nodes: [] }
            ? (SyntaxTree.Parse(new SourceText(block.Name, FragmentHeader + block.Text)).Diagnostics, 1)
            : (tree.Diagnostics, 0);
    }

    private static string Describe(CodeBlock block, int headerLines, Diagnostic diagnostic) =>
        $"{block.Document}:{block.Line + 1 + diagnostic.Location.Start.Line - headerLines}: {diagnostic.Code} {diagnostic.Message}";

    [GeneratedRegex(@"^PIB\d{4}$")]
    private static partial Regex Code();
}
