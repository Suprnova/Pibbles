using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Tests.Runtime;

public class StateSnapshotTests
{
    private const string EveryType = """
        @enum mood: calm, tense
        @actor mira
            name: Mira
            poses: neutral, happy
        @actor rex
            name: Rex
        @var $flag = false
        @var $number = 0
        @var $text = ""
        @var $delay = 0s
        @var $mood: mood = calm
        @var $who: actor = rex
        @var $next: node = t.n

        == t.n
        @set $flag = true
        @set $number = 0.1000000000000000000000000001
        @set $text = "back\\slash \"quoted\""
        @set $delay = 1.0000001s
        @set $mood = tense
        @set $who = mira
        @set $next = t.other
        mira (happy): Hello. #id:l1
        @sequence #id:b1
            - One. #id:l2
            - Two. #id:l3
        -> Pick #id:o1
        -> Leave #id:o2

        == t.other
        Other. #id:l4
        """;

    [Fact]
    public void CreateSnapshot_EveryField_RoundTripsThroughJson()
    {
        Game game = Game.Of(EveryType, declarations: false);
        game.Play("t.n", "Pick");
        (StateSnapshot snapshot, IReadOnlyList<SaveProblem> problems) = game.State.CreateSnapshot();

        string json = SnapshotJson.Serialize(snapshot);
        (StoryState restored, IReadOnlyList<SaveProblem> restoreProblems) = StoryState.Restore(game.Story, SnapshotJson.DeserializeState(json));

        Assert.Empty(problems);
        Assert.Empty(restoreProblems);
        Assert.Equal(json, SnapshotJson.Serialize(restored.CreateSnapshot().Value));
        Assert.Equal(game.State.Seed, restored.Seed);
        Assert.Equal(game.State.Visits, restored.Visits);
        Assert.Equal(game.State.BlockEntries, restored.BlockEntries);
        Assert.Equal(game.State.ChosenOptions, restored.ChosenOptions);
        Assert.Equal("happy", restored.GetPose("mira"));
    }

    [Theory]
    [InlineData("flag", "true")]
    [InlineData("number", "0.1000000000000000000000000001")]
    [InlineData("text", "\"back\\slash \"quoted\"\"")]
    [InlineData("delay", "1.0000001s")]
    [InlineData("mood", "tense")]
    [InlineData("who", "mira")]
    [InlineData("next", "t.other")]
    public void Restore_ValueOfEachType_IsExactlyTheSavedValue(string variable, string expected)
    {
        Game game = Game.Of(EveryType, declarations: false);
        game.Play("t.n", "Pick");

        Game loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems);

        Assert.Empty(problems);
        Assert.Equal(expected, Game.Show(loaded.Get(variable)));
    }

    [Fact]
    public void CreateSnapshot_StateKeyedByFallbackIds_LeavesItOutAndReportsIt()
    {
        Game game = Game.Of("== t.n\n@once\n    Once.\n-> Pick\n-> Keep #id:o2\n", declarations: false);
        game.Play("t.n", "Pick");

        (StateSnapshot snapshot, IReadOnlyList<SaveProblem> problems) = game.State.CreateSnapshot();

        Assert.Empty(snapshot.BlockEntries);
        Assert.Empty(snapshot.ChosenOptions);
        Assert.Equal([SaveProblemKind.FallbackId, SaveProblemKind.FallbackId], problems.Select(problem => problem.Kind));
        Assert.Equal(["~story.pib:2", "~story.pib:4"], problems.Select(problem => problem.Subject));
        Assert.All(problems, problem => Assert.Contains("pibbles ids", problem.Message));
    }

    [Fact]
    public void Restore_VariableRemoved_DropsItAndKeepsTheOthers()
    {
        IReadOnlyList<SaveProblem> problems = Reload("@var $a = 0\n@var $b = 0\n== t.n\n@set $a = 5\n@set $b = 6\n", "@var $b = 0\n== t.n\n", out Game loaded);

        Assert.Equal([(SaveProblemKind.UnknownVariable, "a")], Kinds(problems));
        Assert.Equal(6m, loaded.State.GetVariable("b"));
    }

    [Fact]
    public void Restore_VariableTypeChanged_KeepsItsStartingValue()
    {
        IReadOnlyList<SaveProblem> problems = Reload("@var $a = 0\n== t.n\n@set $a = 5\n", "@var $a = \"start\"\n== t.n\n", out Game loaded);

        Assert.Equal([(SaveProblemKind.VariableTypeChanged, "a")], Kinds(problems));
        Assert.Equal("start", loaded.State.GetVariable("a"));
    }

    [Fact]
    public void Restore_EnumMemberRemoved_KeepsTheStartingValue()
    {
        IReadOnlyList<SaveProblem> problems = Reload(
            "@enum mood: calm, tense, wild\n@var $m: mood = calm\n== t.n\n@set $m = wild\n",
            "@enum mood: calm, tense\n@var $m: mood = calm\n== t.n\n",
            out Game loaded);

        Assert.Equal([(SaveProblemKind.InvalidValue, "m")], Kinds(problems));
        Assert.Contains("`wild`", problems[0].Message);
        Assert.Equal("calm", loaded.State.GetVariable("m"));
    }

    [Fact]
    public void Restore_NodeRenamedWithWas_KeepsItsVisits()
    {
        IReadOnlyList<SaveProblem> problems = Reload("== t.old\n", "== t.new #was:t.old\n", out Game loaded, game => game.Play("t.old"), game => game.Play("t.old"));

        Assert.Empty(problems);
        Assert.Equal(2, loaded.State.Visits["t.new"]);
    }

    [Fact]
    public void Restore_TwoSavedNamesLandOnOneNode_SumsTheirVisitsAndReportsIt()
    {
        IReadOnlyList<SaveProblem> problems = Reload("== t.a\n== t.b\n", "== t.b #was:t.a\n", out Game loaded, game => game.Play("t.a"), game => game.Play("t.b"), game => game.Play("t.b"));

        Assert.Equal([(SaveProblemKind.VisitsMerged, "t.b")], Kinds(problems));
        Assert.Equal(3, loaded.State.Visits["t.b"]);
    }

    [Fact]
    public void Restore_NodeRemoved_DropsItsVisits()
    {
        IReadOnlyList<SaveProblem> problems = Reload("== t.a\n== t.b\n", "== t.b\n", out Game loaded, game => game.Play("t.a"), game => game.Play("t.b"));

        Assert.Equal([(SaveProblemKind.UnknownNode, "t.a")], Kinds(problems));
        Assert.Equal(["t.b"], loaded.State.Visits.Keys);
    }

    [Fact]
    public void Restore_BlockRemoved_DropsItsCount()
    {
        IReadOnlyList<SaveProblem> problems = Reload("== t.n\n@once #id:b1\n    Once. #id:l1\n", "== t.n\nNo block. #id:l1\n", out Game loaded);

        Assert.Equal([(SaveProblemKind.UnknownBlock, "b1")], Kinds(problems));
        Assert.Empty(loaded.State.BlockEntries);
    }

    [Fact]
    public void Restore_BlockMovedToAnotherNode_KeepsItsCount()
    {
        const string After = "== t.n\n== t.m\n@sequence #id:b1\n    - First. #id:l1\n    - Second. #id:l2\n";

        IReadOnlyList<SaveProblem> problems = Reload("== t.n\n@sequence #id:b1\n    - First. #id:l1\n    - Second. #id:l2\n", After, out Game loaded);

        Assert.Empty(problems);
        Assert.Equal(["Line: Second.", "End"], loaded.Play("t.m"));
    }

    [Fact]
    public void Restore_ChosenOptionRemoved_DropsIt()
    {
        IReadOnlyList<SaveProblem> problems = Reload("== t.n\n-> Gone #id:o1\n-> Kept #id:o2\n", "== t.n\n-> Kept #id:o2\n", out Game loaded, game => game.Play("t.n", "Gone"), game => game.Play("t.n", "Kept"));

        Assert.Equal([(SaveProblemKind.UnknownOption, "o1")], Kinds(problems));
        Assert.Equal(["o2"], loaded.State.ChosenOptions);
    }

    [Fact]
    public void Restore_PoseRemoved_KeepsTheDefaultPose()
    {
        const string Before = "@actor mira\n    name: Mira\n    poses: neutral, happy\n@actor rex\n    name: Rex\n    poses: calm, cross\n== t.n\nmira (happy): Hi. #id:l1\nrex (cross): Hi. #id:l2\n";
        const string After = "@actor mira\n    name: Mira\n    poses: neutral\n== t.n\n";

        IReadOnlyList<SaveProblem> problems = Reload(Before, After, out Game loaded);

        Assert.Equal([(SaveProblemKind.UnknownPose, "mira"), (SaveProblemKind.UnknownActor, "rex")], Kinds(problems));
        Assert.Equal("neutral", loaded.State.GetPose("mira"));
    }

    [Fact]
    public void Restore_VariableTheSaveDoesNotHave_GetsItsStartingValue()
    {
        IReadOnlyList<SaveProblem> problems = Reload("@var $a = 0\n== t.n\n@set $a = 5\n", "@var $a = 0\n@var $added = 7\n== t.n\n", out Game loaded);

        Assert.Empty(problems);
        Assert.Equal(5m, loaded.State.GetVariable("a"));
        Assert.Equal(7m, loaded.State.GetVariable("added"));
    }

    [Theory]
    [InlineData(StateSnapshot.CurrentFormat + 1)]
    [InlineData(int.MaxValue)]
    public void Restore_NewerFormat_Throws(int format)
    {
        Story story = Game.Compile("== t.n\n");
        StateSnapshot snapshot = new StoryState(story, 1).CreateSnapshot().Value with { Format = format };

        var exception = Assert.Throws<ArgumentException>(() => StoryState.Restore(story, snapshot));

        Assert.Contains($"save format {format}", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restore_FormatBelowOne_Throws(int format)
    {
        Story story = Game.Compile("== t.n\n");
        StateSnapshot snapshot = new StoryState(story, 1).CreateSnapshot().Value with { Format = format };

        Assert.Throws<ArgumentException>(() => StoryState.Restore(story, snapshot));
    }

    [Fact]
    public void DeserializeState_MissingField_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => SnapshotJson.DeserializeState("""{"format":1,"seed":1}"""));
    }

    /// <summary>Plays <paramref name="before"/> (from <c>t.n</c>, unless <paramref name="plays"/> says otherwise), then saves it and loads it into <paramref name="after"/>.</summary>
    private static IReadOnlyList<SaveProblem> Reload(string before, string after, out Game loaded, params Action<Game>[] plays)
    {
        Game game = Game.Of(before, declarations: false);
        foreach (Action<Game> play in plays.Length > 0 ? plays : [g => g.Play("t.n")])
            play(game);

        loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems, Game.Compile(after));
        return problems;
    }

    private static (SaveProblemKind, string)[] Kinds(IEnumerable<SaveProblem> problems) => [.. problems.Select(problem => (problem.Kind, problem.Subject))];
}
