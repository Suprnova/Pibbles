using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Documentation;

public partial class CodeBlockTests
{
    private const string FragmentHeader = "== doc.example\n";

    private static readonly string LanguageFolder = Path.Combine(RepositoryRoot.Path, "docs", "language");

    private static readonly string[] Documents = ["reference.md", "guide.md"];

    private static readonly Dictionary<string, CodeBlock> Blocks = Documents.SelectMany(Extract).ToDictionary(block => block.Name);

    public static TheoryData<string> LanguageDocuments { get; } = [.. Documents];

    public static TheoryData<string> PibblesBlocks { get; } = [.. Blocks.Values.Where(block => block.Info.StartsWith("pib", StringComparison.Ordinal) && HasValidInfo(block)).Select(block => block.Name)];

    [Theory]
    [MemberData(nameof(LanguageDocuments))]
    public void CodeBlocks_InLanguageDocument_HaveValidInfoStrings(string document)
    {
        string[] invalid = [.. Blocks.Values.Where(block => block.Document == document && !HasValidInfo(block)).Select(block => $"line {block.Line}: `{block.Info}`")];

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
        var source = new SourceText("examples.pib", File.ReadAllText(Path.Combine(LanguageFolder, "examples.pib")));

        Assert.Empty(SyntaxTree.Parse(source).Diagnostics);
    }

    private static IEnumerable<CodeBlock> Extract(string document)
    {
        string text = File.ReadAllText(Path.Combine(LanguageFolder, document));

        return Fence().Matches(text).Select(match => new CodeBlock(document, LineOf(text, match.Index), match.Groups["info"].Value, match.Groups["body"].Value));
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

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private sealed record CodeBlock(string Document, int Line, string Info, string Text)
    {
        public string Name => $"{Document}:{Line}";
    }

    // Pairs each fence with its closer, so only opening fences carry an info string.
    [GeneratedRegex(@"^```(?<info>[^\r\n]*)\r?\n(?<body>(?s:.*?))^```\r?$", RegexOptions.Multiline)]
    private static partial Regex Fence();

    [GeneratedRegex(@"^PIB\d{4}$")]
    private static partial Regex Code();
}
