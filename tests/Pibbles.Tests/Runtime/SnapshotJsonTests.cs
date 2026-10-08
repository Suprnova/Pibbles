using System.Text.Json;
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
        @actor mira
            name: Mira
            poses: neutral, happy
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

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private static string Indented(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, IndentedOptions);
    }
}
