using System.Text.RegularExpressions;

namespace Pibbles.Tests.Documentation;

/// <summary>A fenced code block in the language reference or the writer's guide.</summary>
/// <param name="Document">The document's file name, such as <c>reference.md</c>.</param>
/// <param name="Line">The 1-based line of the opening fence.</param>
/// <param name="Info">The fence's info string, such as <c>pib</c> or <c>pib-error PIB1061</c>.</param>
/// <param name="Text">The block's text, without its fences.</param>
internal sealed partial record CodeBlock(string Document, int Line, string Info, string Text)
{
    public static string Folder { get; } = Path.Combine(RepositoryRoot.Path, "docs", "language");

    public static string[] Documents { get; } = ["reference.md", "guide.md"];

    public static IReadOnlyList<CodeBlock> All { get; } = [.. Documents.SelectMany(Extract)];

    public string Name => $"{Document}:{Line}";

    private static IEnumerable<CodeBlock> Extract(string document)
    {
        string text = File.ReadAllText(Path.Combine(Folder, document));

        return Fence().Matches(text).Select(match => new CodeBlock(document, LineOf(text, match.Index), match.Groups["info"].Value, match.Groups["body"].Value));
    }

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    // Pairs each fence with its closer, so only opening fences carry an info string.
    [GeneratedRegex(@"^```(?<info>[^\r\n]*)\r?\n(?<body>(?s:.*?))^```\r?$", RegexOptions.Multiline)]
    private static partial Regex Fence();
}
