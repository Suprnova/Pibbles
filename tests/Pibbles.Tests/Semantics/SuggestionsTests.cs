using Pibbles.Semantics;

namespace Pibbles.Tests.Semantics;

public class SuggestionsTests
{
    public static TheoryData<string, string[], string?> Cases { get; } = new()
    {
        { "thougth", ["thought", "show_disabled"], "thought" },
        { "postion", ["position", "portion"], "position" },
        { "Position", ["position"], "position" },
        { "ab", ["ac"], "ac" },
        { "ab", ["xy"], null },
        { "window", ["wind"], "wind" },
        { "window", ["win"], null },
        { "fridge", ["position", "room"], null },
        { "anything", [], null },
        { "\U00020BB7\U00020BB7\U00020BB7", ["\U00020BB8\U00020BB7\U00020BB7"], "\U00020BB8\U00020BB7\U00020BB7" },
        { "\U00020BB7\U00020BB7\U00020BB7", ["\U00020BB8\U00020BB8\U00020BB7"], null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Closest_Name_ReturnsNearestCandidateWithinLimit(string name, string[] candidates, string? expected) =>
        Assert.Equal(expected, Suggestions.Closest(name, candidates));
}
