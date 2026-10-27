using System.Text.Json;
using Pibbles.Compiler;
using Pibbles.Runtime;
using static VerifyXunit.Verifier;

namespace Pibbles.Tests.Runtime;

/// <summary>
/// The JSON of a representative pair of snapshots, pinned: it's a save-file format, so any change to it must be
/// deliberate, and a newer core must still read what an older one wrote.
/// </summary>
public class SnapshotJsonTests
{
    private const string Story = """
        @enum mood: calm, tense
        @channel pose
        @actor mira
            name: Mira
            pose: neutral, happy
        @var $flag = false
        @var $number = 0
        @var $text = ""
        @var $delay = 0s
        @var $mood: mood = calm
        @var $who: actor = mira
        @var $next: node = t.n

        == t.n
        @set $flag = true
        @set $number = 1.50
        @set $text = "a \"quote\""
        @set $delay = 0.25s
        @set $mood = tense
        @set $next = t.sub
        mira (happy): Hello. #id:l1
        @once #id:b1
            -> Pick #id:o1
            -> Never  @if false #id:o2
        @call t.sub #id:c1

        == t.sub
        -> Stay #id:o3
        -> Go  @once #id:o4
        """;

    [Fact]
    public Task Serialize_RepresentativeSnapshots_MatchesTheVerifiedSaveFormat()
    {
        Game game = Game.Of(Story, declarations: false);
        game.Play("t.n", "Pick");

        string json = $"{Indented(SnapshotJson.Serialize(game.State.CreateSnapshot().Value))}\n{Indented(SnapshotJson.Serialize(game.Runner.CreateSnapshot().Value))}\n";

        return Verify(json).UseFileName("save-format");
    }

    [Fact]
    public void Deserialize_WhatSerializeWrote_GivesTheSameJson()
    {
        Game game = Game.Of(Story, declarations: false);
        game.Play("t.n", "Pick");
        string state = SnapshotJson.Serialize(game.State.CreateSnapshot().Value);
        string runner = SnapshotJson.Serialize(game.Runner.CreateSnapshot().Value);

        Assert.Equal(state, SnapshotJson.Serialize(SnapshotJson.DeserializeState(state)));
        Assert.Equal(runner, SnapshotJson.Serialize(SnapshotJson.DeserializeRunner(runner)));
    }

    [Fact]
    public void Deserialize_Null_Throws()
    {
        Assert.Throws<JsonException>(() => SnapshotJson.DeserializeState("null"));
        Assert.Throws<JsonException>(() => SnapshotJson.DeserializeRunner("null"));
    }

    [Theory]
    [InlineData("variables", """{"format":1,"seed":1,"variables":null,"visits":{},"blockEntries":{},"chosenOptions":[],"channels":{}}""")]
    [InlineData("variables.x", """{"format":1,"seed":1,"variables":{"x":null},"visits":{},"blockEntries":{},"chosenOptions":[],"channels":{}}""")]
    [InlineData("variables.x.type", """{"format":1,"seed":1,"variables":{"x":{"type":null,"number":1}},"visits":{},"blockEntries":{},"chosenOptions":[],"channels":{}}""")]
    [InlineData("visits", """{"format":1,"seed":1,"variables":{},"visits":null,"blockEntries":{},"chosenOptions":[],"channels":{}}""")]
    [InlineData("blockEntries", """{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":null,"chosenOptions":[],"channels":{}}""")]
    [InlineData("chosenOptions", """{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":{},"chosenOptions":null,"channels":{}}""")]
    [InlineData("chosenOptions[1]", """{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":{},"chosenOptions":["o1",null],"channels":{}}""")]
    [InlineData("channels", """{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":{},"chosenOptions":[],"channels":null}""")]
    [InlineData("channels.mira", """{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":{},"chosenOptions":[],"channels":{"mira":null}}""")]
    [InlineData("channels.mira.pose", """{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":{},"chosenOptions":[],"channels":{"mira":{"pose":null}}}""")]
    public void DeserializeState_NullWhereAValueMustBe_ThrowsNamingThePath(string path, string json)
    {
        var exception = Assert.Throws<JsonException>(() => SnapshotJson.DeserializeState(json));

        Assert.Contains($"`{path}`", exception.Message);
    }

    [Theory]
    [InlineData("""{"format":1,"seed":1,"variables":{},"visits":{"t.n":null},"blockEntries":{},"chosenOptions":[],"channels":{}}""")]
    [InlineData("""{"format":1,"seed":1,"variables":{},"visits":{},"blockEntries":{"b1":null},"chosenOptions":[],"channels":{}}""")]
    [InlineData("""{"format":1,"seed":null,"variables":{},"visits":{},"blockEntries":{},"chosenOptions":[],"channels":{}}""")]
    public void DeserializeState_NullCount_Throws(string json)
    {
        Assert.Throws<JsonException>(() => SnapshotJson.DeserializeState(json));
    }

    [Theory]
    [InlineData("calls", """{"format":1,"calls":null,"line":"l1","choice":[]}""")]
    [InlineData("calls[0]", """{"format":1,"calls":[null],"line":"l1","choice":[]}""")]
    [InlineData("choice", """{"format":1,"calls":[],"choice":null}""")]
    [InlineData("choice[1]", """{"format":1,"calls":[],"choice":["o1",null]}""")]
    public void DeserializeRunner_NullWhereAValueMustBe_ThrowsNamingThePath(string path, string json)
    {
        var exception = Assert.Throws<JsonException>(() => SnapshotJson.DeserializeRunner(json));

        Assert.Contains($"`{path}`", exception.Message);
    }

    [Fact]
    public void DeserializeRunner_NullLine_IsAChoiceOrNoDialogue()
    {
        RunnerSnapshot snapshot = SnapshotJson.DeserializeRunner("""{"format":1,"calls":[],"line":null,"choice":[]}""");

        Assert.False(snapshot.HasDialogue);
    }

    public static TheoryData<string, StateSnapshot> NullStates { get; } = new()
    {
        { "variables", EmptyState with { Variables = null! } },
        { "variables.x", EmptyState with { Variables = new Dictionary<string, SavedValue> { ["x"] = null! } } },
        { "variables.x.type", EmptyState with { Variables = new Dictionary<string, SavedValue> { ["x"] = new() { Type = null! } } } },
        { "visits", EmptyState with { Visits = null! } },
        { "blockEntries", EmptyState with { BlockEntries = null! } },
        { "chosenOptions", EmptyState with { ChosenOptions = null! } },
        { "chosenOptions[0]", EmptyState with { ChosenOptions = [null!] } },
        { "channels", EmptyState with { Channels = null! } },
        { "channels.mira", EmptyState with { Channels = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["mira"] = null! } } },
        { "channels.mira.pose", EmptyState with { Channels = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["mira"] = new Dictionary<string, string> { ["pose"] = null! } } } },
    };

    public static TheoryData<string, RunnerSnapshot> NullRunners { get; } = new()
    {
        { "calls", RunnerSnapshot.Empty with { Calls = null! } },
        { "calls[0]", RunnerSnapshot.Empty with { Calls = [null!], Line = "l1" } },
        { "choice", RunnerSnapshot.Empty with { Choice = null! } },
        { "choice[0]", RunnerSnapshot.Empty with { Choice = [null!] } },
    };

    private static StateSnapshot EmptyState => new() { Format = 1, Seed = 1, Variables = new Dictionary<string, SavedValue>(), Visits = new Dictionary<string, int>(), BlockEntries = new Dictionary<string, int>(), ChosenOptions = [], Channels = new Dictionary<string, IReadOnlyDictionary<string, string>>() };

    [Theory]
    [MemberData(nameof(NullStates))]
    public void RestoreState_NullWhereAValueMustBe_ThrowsNamingThePath(string path, StateSnapshot snapshot)
    {
        Story story = Game.Compile("== t.n\n");

        var exception = Assert.Throws<ArgumentException>(() => StoryState.Restore(story, snapshot));

        Assert.Contains($"`{path}`", exception.Message);
    }

    [Theory]
    [MemberData(nameof(NullRunners))]
    public void RestoreRunner_NullWhereAValueMustBe_ThrowsNamingThePath(string path, RunnerSnapshot snapshot)
    {
        Game game = Game.Of("== t.n\n", declarations: false);

        var exception = Assert.Throws<ArgumentException>(() => DialogueRunner.Restore(game.Story, game.State, game.Functions, snapshot));

        Assert.Contains($"`{path}`", exception.Message);
    }

    [Fact]
    public void Restore_NullSnapshot_Throws()
    {
        Game game = Game.Of("== t.n\n", declarations: false);

        Assert.Throws<ArgumentNullException>(() => StoryState.Restore(game.Story, null!));
        Assert.Throws<ArgumentNullException>(() => DialogueRunner.Restore(game.Story, game.State, game.Functions, null!));
    }

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private static string Indented(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, IndentedOptions);
    }
}
