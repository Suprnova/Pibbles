using System.Text.RegularExpressions;

namespace Pibbles.Tests.Documentation;

public partial class CodeBlockTests
{
    public static TheoryData<string> LanguageDocuments { get; } = ["reference.md", "guide.md"];

    [Theory]
    [MemberData(nameof(LanguageDocuments))]
    public void CodeBlocks_InLanguageDocument_AreAllTagged(string document)
    {
        string text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "language", document));

        string[] untagged = [.. OpeningFence().Matches(text).Where(match => match.Groups["info"].Value.Length == 0).Select(match => $"line {LineOf(text, match.Index)}")];

        Assert.Empty(untagged);
    }

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    // Pairs each fence with its closer, so only opening fences carry an info string.
    [GeneratedRegex(@"^```(?<info>[^\r\n]*)\r?$(?s:.*?)^```\r?$", RegexOptions.Multiline)]
    private static partial Regex OpeningFence();
}
