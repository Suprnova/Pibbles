using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Documentation;

public partial class CodeBlockTests
{
    private const string FragmentHeader = "== doc.example\n";

    private static readonly Dictionary<string, CodeBlock> Blocks = CodeBlock.All.ToDictionary(block => block.Name);

    /// <summary>The documentation prelude, which declares every name the <c>pib</c> blocks use without declaring it.</summary>
    private static readonly SourceText Prelude = new("examples.pib", File.ReadAllText(Path.Combine(CodeBlock.Folder, "examples.pib")));

    public static TheoryData<string> LanguageDocuments { get; } = [.. CodeBlock.Documents];

    public static TheoryData<string> PibblesBlocks { get; } = [.. CodeBlock.All.Where(block => block.Info.StartsWith("pib", StringComparison.Ordinal) && HasValidInfo(block)).Select(block => block.Name)];

    [Theory]
    [MemberData(nameof(LanguageDocuments))]
    public void CodeBlocks_InLanguageDocument_HaveValidInfoStrings(string document)
    {
        string[] invalid = [.. CodeBlock.All.Where(block => block.Document == document && !HasValidInfo(block)).Select(block => $"line {block.Line}: `{block.Info}`")];

        Assert.Empty(invalid);
    }

    /// <summary>
    /// A <c>pib</c> or <c>pib-standalone</c> block has no errors or warnings. A <c>pib-error</c> block has exactly the codes
    /// its info string lists, in order. Examples leave out line IDs, so missing ones are never counted.
    /// </summary>
    [Theory]
    [MemberData(nameof(PibblesBlocks))]
    public void Compile_CodeBlock_ReportsCodesInItsInfoString(string name)
    {
        var block = Blocks[name];
        bool error = block.Info.StartsWith("pib-error", StringComparison.Ordinal);
        string[] expected = [.. block.Info.Split(' ').Skip(1)];

        var (diagnostics, headerLines) = Compile(block);
        Diagnostic[] reported = [.. diagnostics.Where(diagnostic => diagnostic.Code is not "PIB3010" && (error || diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))];

        Assert.True(reported.Select(diagnostic => diagnostic.Code).SequenceEqual(expected),
            $"Expected [{string.Join(", ", expected)}], got:\n{string.Join("\n", reported.Select(diagnostic => Describe(block, headerLines, diagnostic)))}");
    }

    /// <summary>The prelude's variables are for the blocks to use, so on its own nothing uses them.</summary>
    [Fact]
    public void Compile_DocumentationPrelude_ReportsOnlyMissingIdsAndUnusedVariables() =>
        Assert.All(Compilation.Create([Prelude]).Diagnostics, diagnostic => Assert.Contains(diagnostic.Code, (string[])["PIB3010", "PIB5040"]));

    private static bool HasValidInfo(CodeBlock block) => block.Info.Split(' ') switch
    {
        ["pib" or "pib-standalone"] => true,
        ["pib-error", .. var codes] => codes.Length > 0 && codes.All(code => Code().IsMatch(code)),
        _ => block.Info.Length > 0 && !block.Info.StartsWith("pib", StringComparison.Ordinal),
    };

    /// <summary>
    /// Compiles a block, with the documentation prelude unless it's <c>pib-standalone</c>, first wrapping it in a node if
    /// it's a fragment: a block with no prefix, declaration or node. Returns only the block's own diagnostics.
    /// </summary>
    private static (IReadOnlyList<Diagnostic> Diagnostics, int HeaderLines) Compile(CodeBlock block)
    {
        bool fragment = SyntaxTree.Parse(new SourceText(block.Name, block.Text)).Root is { Prefix: null, Declarations: [], Nodes: [] };
        var source = new SourceText(block.Name, fragment ? FragmentHeader + block.Text : block.Text);
        SourceText[] sources = block.Info is "pib-standalone" ? [source] : [Prelude, source];

        return ([.. Compilation.Create(sources).Diagnostics.Where(diagnostic => diagnostic.Location.Path == block.Name)], fragment ? 1 : 0);
    }

    private static string Describe(CodeBlock block, int headerLines, Diagnostic diagnostic) =>
        $"{block.Document}:{block.Line + 1 + diagnostic.Location.Start.Line - headerLines}: {diagnostic.Code} {diagnostic.Message}";

    [GeneratedRegex(@"^PIB\d{4}$")]
    private static partial Regex Code();
}
