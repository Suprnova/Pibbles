using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Tests.Runtime;

public class RunnerSnapshotTests
{
    private const string Nested = """
        == t.n
        mira: Start. #id:l1
        @call t.sub #id:c1
        mira: Back. #id:l2
        == t.sub
        @call t.deep #id:c2
        mira: Sub end. #id:l3
        == t.deep
        mira: Deep. #id:l4
        """;

    [Fact]
    public void CreateSnapshot_AfterALine_SavesItAndRestoreShowsItAgain()
    {
        Game game = Game.Of("== t.n\nmira: One. #id:l1\nmira: Two. #id:l2\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        RunnerSnapshot snapshot = game.Runner.CreateSnapshot().Value;
        Game loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems);

        Assert.Equal("l1", snapshot.Line);
        Assert.Empty(snapshot.Calls);
        Assert.Empty(problems);
        Assert.Equal(["Line mira: One.", "Line mira: Two.", "End"], loaded.Continue());
    }

    [Fact]
    public void CreateSnapshot_AtAChoice_SavesEveryOptionAndRestoreOffersItAgain()
    {
        Game game = Game.Of("== t.n\n-> A #id:o1\n-> B  @if $key #id:o2\n-> C  @once #id:o3\nmira: After. #id:l1\n");
        game.Play("t.n", "C");
        game.Runner.Start("t.n");
        game.Runner.Next();

        RunnerSnapshot snapshot = game.Runner.CreateSnapshot().Value;
        Game loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems);

        Assert.Equal(["o1", "o2", "o3"], snapshot.Choice);
        Assert.Null(snapshot.Line);
        Assert.Empty(problems);
        Assert.Equal(["Choice A | B (unavailable)", "Chose A", "Line mira: After.", "End"], loaded.Continue("A"));
    }

    [Fact]
    public void CreateSnapshot_InsideNestedCalls_SavesTheCallsAndRestoreReturnsThroughThem()
    {
        Game game = Game.Of(Nested);
        game.Runner.Start("t.n");
        game.Runner.Next();
        game.Runner.Next();
        Dictionary<string, int> visits = new(game.State.Visits);

        RunnerSnapshot snapshot = game.Runner.CreateSnapshot().Value;
        Game loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems);

        Assert.Equal(["c1", "c2"], snapshot.Calls);
        Assert.Equal("l4", snapshot.Line);
        Assert.Empty(problems);
        Assert.Equal(visits, loaded.State.Visits);
        Assert.Equal(["Line mira: Deep.", "Line mira: Sub end.", "Line mira: Back.", "End"], loaded.Continue());
        Assert.Equal(visits, loaded.State.Visits);
    }

    [Fact]
    public void CreateSnapshot_NeverStarted_IsEmptyAndRestoresEnded()
    {
        Game game = Game.Of("== t.n\nmira: One. #id:l1\n");

        RunnerSnapshot snapshot = game.Runner.CreateSnapshot().Value;
        Game loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems);

        Assert.False(snapshot.HasDialogue);
        Assert.Empty(problems);
        Assert.IsType<EndStep>(loaded.Runner.Next());
    }

    [Fact]
    public void CreateSnapshot_AfterTheEnd_IsEmpty()
    {
        Game game = Game.Of("== t.n\nmira: One. #id:l1\n");
        game.Play("t.n");

        (RunnerSnapshot snapshot, IReadOnlyList<SaveProblem> problems) = game.Runner.CreateSnapshot();

        Assert.Same(RunnerSnapshot.Empty, snapshot);
        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("@show mira")]
    [InlineData("mira (happy):")]
    [InlineData("@wait 1")]
    public void CreateSnapshot_AfterAStepThatIsNotASavePoint_Throws(string statement)
    {
        Game game = Game.Of($"== t.n\n{statement}\nmira: Line. #id:l1\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        var exception = Assert.Throws<InvalidOperationException>(game.Runner.CreateSnapshot);

        Assert.Contains("FastForward", exception.Message);
    }

    [Fact]
    public void CreateSnapshot_AfterStartBeforeNext_Throws()
    {
        Game game = Game.Of("== t.n\nmira: Line. #id:l1\n");
        game.Runner.Start("t.n");

        Assert.Throws<InvalidOperationException>(game.Runner.CreateSnapshot);
    }

    [Fact]
    public void Restore_ChoiceWithNoOptionAvailableNow_SkipsItAndReportsIt()
    {
        Game game = Game.Of("== t.n\n-> A  @if $key #id:o1\nmira: After. #id:l1\n");
        game.State.SetVariable("key", true);
        game.Runner.Start("t.n");
        game.Runner.Next();
        StoryState state = StoryState.Restore(game.Story, game.State.CreateSnapshot().Value).Value;
        state.SetVariable("key", false);

        (DialogueRunner runner, IReadOnlyList<SaveProblem> problems) = DialogueRunner.Restore(game.Story, state, game.Functions, game.Runner.CreateSnapshot().Value);

        Assert.Equal([(SaveProblemKind.NoOptionAvailable, "o1")], Kinds(problems));
        Assert.Equal("Line mira: After.", Game.Format(runner.Next()));
    }

    [Theory]
    [InlineData(RunnerSnapshot.CurrentFormat + 1)]
    [InlineData(0)]
    public void Restore_FormatItCannotRead_Throws(int format)
    {
        Game game = Game.Of("== t.n\nmira: Line. #id:l1\n");

        Assert.Throws<ArgumentException>(() => DialogueRunner.Restore(game.Story, game.State, game.Functions, RunnerSnapshot.Empty with { Format = format }));
    }

    [Fact]
    public void Restore_RewordedLine_ShowsTheNewWording()
    {
        Game loaded = SaveAtFirstLine("== t.n\nmira: One. #id:l1\nmira: Two. #id:l2\n", "== t.n\nmira: Uno. #id:l1\nmira: Dos. #id:l2\n", out IReadOnlyList<SaveProblem> problems);

        Assert.Empty(problems);
        Assert.Equal(["Line mira: Uno.", "Line mira: Dos.", "End"], loaded.Continue());
    }

    [Fact]
    public void Restore_LineMovedToAnotherNode_ResumesThereAndFollowsItsNewFlow()
    {
        Game loaded = SaveAtFirstLine("== t.n\nmira: One. #id:l1\nmira: Two. #id:l2\n", "== t.n\nmira: Two. #id:l2\n== t.other\nmira: One. #id:l1\nmira: Elsewhere. #id:l3\n", out IReadOnlyList<SaveProblem> problems);

        Assert.Empty(problems);
        Assert.Equal(["Line mira: One.", "Line mira: Elsewhere.", "End"], loaded.Continue());
    }

    [Fact]
    public void Restore_LineDeleted_LosesTheFrameAndEnds()
    {
        Game loaded = SaveAtFirstLine("== t.n\nmira: One. #id:l1\nmira: Two. #id:l2\n", "== t.n\nmira: Two. #id:l2\n", out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.LostFrame, "l1")], Kinds(problems));
        Assert.Equal(["End"], loaded.Continue());
    }

    [Fact]
    public void Restore_OneOptionDeleted_OffersTheRest()
    {
        Game loaded = SaveAtFirstChoice("== t.n\n-> A #id:o1\n-> B #id:o2\n-> C #id:o3\n", "== t.n\n-> A #id:o1\n-> C #id:o3\n", out IReadOnlyList<SaveProblem> problems);

        Assert.Empty(problems);
        Assert.Equal(["Choice A | C", "Chose C", "End"], loaded.Continue("C"));
    }

    [Fact]
    public void Restore_OptionsReordered_OffersThemInTheNewOrder()
    {
        Game loaded = SaveAtFirstChoice("== t.n\n-> A #id:o1\n-> B #id:o2\n", "== t.n\n-> B #id:o2\n-> A #id:o1\n", out IReadOnlyList<SaveProblem> problems);

        Assert.Empty(problems);
        Assert.Equal(["Choice B | A", "Chose A", "End"], loaded.Continue("A"));
    }

    [Fact]
    public void Restore_AllOptionsDeleted_LosesTheFrameAndReturnsFromTheCall()
    {
        const string Before = "== t.n\n@call t.sub #id:c1\nmira: After. #id:l9\n== t.sub\n-> A #id:o1\n-> B #id:o2\n";
        const string After = "== t.n\n@call t.sub #id:c1\nmira: After. #id:l9\n== t.sub\nmira: Nothing. #id:l5\n";

        Game loaded = SaveAtFirstChoice(Before, After, out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.LostFrame, "o1")], Kinds(problems));
        Assert.Contains("`o1`, `o2`", problems[0].Message);
        Assert.Equal(["Line mira: After.", "End"], loaded.Continue());
    }

    [Fact]
    public void Restore_OptionsSplitAcrossTwoChoices_ResumesAtTheOneWithMostAndReportsIt()
    {
        const string After = "== t.n\n-> A #id:o1\nmira: Between. #id:l1\n-> B #id:o2\n-> C #id:o3\n";

        Game loaded = SaveAtFirstChoice("== t.n\n-> A #id:o1\n-> B #id:o2\n-> C #id:o3\n", After, out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.ChoiceSplit, "o2")], Kinds(problems));
        Assert.Equal(["Choice B | C", "Chose B", "End"], loaded.Continue("B"));
    }

    [Fact]
    public void Restore_OptionsSplitEvenly_ResumesAtTheEarliestInSavedOrder()
    {
        const string After = "== t.n\n-> B #id:o2\nmira: Between. #id:l1\n-> A #id:o1\n";

        Game loaded = SaveAtFirstChoice("== t.n\n-> A #id:o1\n-> B #id:o2\n", After, out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.ChoiceSplit, "o1")], Kinds(problems));
        Assert.Equal(["Choice A", "Chose A", "End"], loaded.Continue("A"));
    }

    [Fact]
    public void Restore_OptionIdNowALine_DoesNotFindTheChoice()
    {
        Game loaded = SaveAtFirstChoice("== t.n\n-> A #id:o1\n", "== t.n\nmira: A. #id:o1\n", out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.LostFrame, "o1")], Kinds(problems));
        Assert.Equal(["End"], loaded.Continue());
    }

    [Fact]
    public void Restore_CallDeleted_DropsThatFrame()
    {
        const string Before = "== t.n\n@call t.sub #id:c1\nmira: After. #id:l9\n== t.sub\nmira: In. #id:l5\nmira: Sub end. #id:l6\n";
        const string After = "== t.n\nmira: After. #id:l9\n== t.sub\nmira: In. #id:l5\nmira: Sub end. #id:l6\n";

        Game loaded = SaveAtFirstLine(Before, After, out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.LostFrame, "c1")], Kinds(problems));
        Assert.Equal(["Line mira: In.", "Line mira: Sub end.", "End"], loaded.Continue());
    }

    [Fact]
    public void CreateSnapshot_LineWithoutAnId_SavesNoDialogueAndReportsIt()
    {
        Game game = Game.Of("== t.n\nmira: No ID.\nmira: Two. #id:l2\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        Game loaded = game.SaveAndLoad(out IReadOnlyList<SaveProblem> problems);

        Assert.Equal([(SaveProblemKind.FallbackId, "~story.pib:" + (Game.Defs.Split('\n').Length + 1))], Kinds(problems));
        Assert.Equal(["End"], loaded.Continue());
    }

    [Fact]
    public void CreateSnapshot_ChoiceWithSomeOptionsWithoutIds_SavesTheOthers()
    {
        Game game = Game.Of("== t.n\n-> A\n-> B #id:o2\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        (RunnerSnapshot snapshot, IReadOnlyList<SaveProblem> problems) = game.Runner.CreateSnapshot();

        Assert.Equal(["o2"], snapshot.Choice);
        Assert.Equal([SaveProblemKind.FallbackId], problems.Select(problem => problem.Kind));
    }

    [Fact]
    public void FastForward_AcrossCommandsPosesAndWaits_StopsAtTheNextLine()
    {
        Game game = Game.Of("== t.n\n@show mira\n@wait 1\nmira (happy): Next. #id:l1\nmira: Later. #id:l2\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        IReadOnlyList<DialogueStep> passed = game.Runner.FastForward();

        Assert.Equal(["Wait 1s", "Pose mira happy", "Line mira: Next."], passed.Select(Game.Format));
        Assert.Equal("l1", game.Runner.CreateSnapshot().Value.Line);
    }

    [Fact]
    public void FastForward_ToAChoice_StopsThere()
    {
        Game game = Game.Of("== t.n\n@show mira\n@show rex\n-> A #id:o1\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        IReadOnlyList<DialogueStep> passed = game.Runner.FastForward();

        Assert.Equal(["Command @show(rex, calm)", "Choice A"], passed.Select(Game.Format));
        Assert.Equal(["o1"], game.Runner.CreateSnapshot().Value.Choice);
    }

    [Fact]
    public void FastForward_ToTheEnd_LeavesNoDialogueToSave()
    {
        Game game = Game.Of("== t.n\n@show mira\n@show rex\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        IReadOnlyList<DialogueStep> passed = game.Runner.FastForward();

        Assert.Equal(["Command @show(rex, calm)", "End"], passed.Select(Game.Format));
        Assert.Same(RunnerSnapshot.Empty, game.Runner.CreateSnapshot().Value);
    }

    [Fact]
    public void FastForward_InsideACall_SavesTheCall()
    {
        Game game = Game.Of("== t.n\n@call t.sub #id:c1\n== t.sub\n@show mira\n@wait 1\nmira: In. #id:l1\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        IReadOnlyList<DialogueStep> passed = game.Runner.FastForward();

        Assert.Equal(["Wait 1s", "Line mira: In."], passed.Select(Game.Format));
        Assert.Equal(["c1"], game.Runner.CreateSnapshot().Value.Calls);
    }

    [Fact]
    public void FastForward_AfterStart_RunsToTheFirstLine()
    {
        Game game = Game.Of("== t.n\n@show mira\nmira: In. #id:l1\n");
        game.Runner.Start("t.n");

        Assert.Equal(["Command @show(mira, calm)", "Line mira: In."], game.Runner.FastForward().Select(Game.Format));
    }

    [Fact]
    public void FastForward_AtASavePoint_ReturnsNothing()
    {
        Game game = Game.Of("== t.n\nmira: One. #id:l1\n-> A #id:o1\n");
        Assert.Empty(game.Runner.FastForward());
        game.Runner.Start("t.n");
        game.Runner.Next();
        Assert.Empty(game.Runner.FastForward());
        game.Runner.Next();
        Assert.Empty(game.Runner.FastForward());
        game.Runner.Choose("o1");
        game.Runner.Next();

        Assert.Empty(game.Runner.FastForward());
    }

    [Fact]
    public void FastForward_StoryThatGivesCommandsForever_EndsWithAWarning()
    {
        Game game = Game.Of("== t.n\n@show mira\n@jump t.n\n", budget: 50);
        game.Runner.Start("t.n");
        game.Runner.Next();

        IReadOnlyList<DialogueStep> passed = game.Runner.FastForward();

        Assert.IsType<EndStep>(passed[^1]);
        Assert.Equal([RuntimeWarningKind.InfiniteLoop], game.Warnings.Select(warning => warning.Kind));
    }

    [Fact]
    public void CreateSnapshot_RightAfterRestoringAtALine_GivesThatLine()
    {
        Game game = Game.Of(Nested);
        game.Runner.Start("t.n");
        game.Runner.Next();
        game.Runner.Next();
        Game loaded = game.SaveAndLoad(out _);

        RunnerSnapshot snapshot = loaded.Runner.CreateSnapshot().Value;

        Assert.Equal(["c1", "c2"], snapshot.Calls);
        Assert.Equal("l4", snapshot.Line);
        Assert.Empty(loaded.Runner.FastForward());
        Assert.Equal("Line mira: Deep.", Game.Format(loaded.Runner.Next()));
    }

    [Fact]
    public void CreateSnapshot_RightAfterRestoringAtAChoice_GivesThatChoice()
    {
        Game game = Game.Of("== t.n\n-> A #id:o1\n-> B #id:o2\n");
        game.Runner.Start("t.n");
        game.Runner.Next();
        Game loaded = game.SaveAndLoad(out _);

        RunnerSnapshot snapshot = loaded.Runner.CreateSnapshot().Value;

        Assert.Equal(["o1", "o2"], snapshot.Choice);
        Assert.Empty(loaded.Runner.FastForward());
        Assert.Equal("Choice A | B", Game.Format(loaded.Runner.Next()));
    }

    [Fact]
    public void CreateSnapshot_RightAfterRestoringWithALostCall_LeavesTheCallOut()
    {
        const string Before = "== t.n\n@call t.sub #id:c1\nmira: After. #id:l9\n== t.sub\nmira: In. #id:l5\n";
        Game loaded = SaveAtFirstLine(Before, "== t.n\nmira: After. #id:l9\n== t.sub\nmira: In. #id:l5\n", out _);

        RunnerSnapshot snapshot = loaded.Runner.CreateSnapshot().Value;

        Assert.Empty(snapshot.Calls);
        Assert.Equal("l5", snapshot.Line);
    }

    [Fact]
    public void CreateSnapshot_RightAfterRestoringPastALostLine_Throws()
    {
        const string Before = "== t.n\n@call t.sub #id:c1\nmira: After. #id:l9\n== t.sub\nmira: In. #id:l5\n";
        Game loaded = SaveAtFirstLine(Before, "== t.n\n@call t.sub #id:c1\nmira: After. #id:l9\n== t.sub\n", out _);

        Assert.Throws<InvalidOperationException>(loaded.Runner.CreateSnapshot);
        Assert.Equal(["Line mira: After."], loaded.Runner.FastForward().Select(Game.Format));
    }

    [Fact]
    public void CreateSnapshot_RightAfterRestoringASkippedChoice_Throws()
    {
        Game game = Game.Of("== t.n\n-> A  @if $key #id:o1\nmira: After. #id:l1\n");
        game.State.SetVariable("key", true);
        game.Runner.Start("t.n");
        game.Runner.Next();
        StoryState state = StoryState.Restore(game.Story, game.State.CreateSnapshot().Value).Value;
        state.SetVariable("key", false);
        DialogueRunner runner = DialogueRunner.Restore(game.Story, state, game.Functions, game.Runner.CreateSnapshot().Value).Value;

        Assert.Throws<InvalidOperationException>(runner.CreateSnapshot);
    }

    [Fact]
    public void SaveAndLoad_TwiceInARow_ResumesAtTheSamePlace()
    {
        Game game = Game.Of(Nested);
        game.Runner.Start("t.n");
        game.Runner.Next();
        game.Runner.Next();

        Game twice = game.SaveAndLoad(out IReadOnlyList<SaveProblem> first).SaveAndLoad(out IReadOnlyList<SaveProblem> second);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(["Line mira: Deep.", "Line mira: Sub end.", "Line mira: Back.", "End"], twice.Continue());
    }

    [Fact]
    public void FastForward_HostFunctionThrowsPartway_HandsOverTheStepsAlreadyPassed()
    {
        Game game = Game.Of(
            "== t.n\n@show mira\n@show rex\n@set $count = 1\n@if has_item(\"x\")\n    mira: Yes. #id:l1\n",
            functions => functions.Add("has_item", (string id) => id == "crowbar" ? true : throw new InvalidOperationException("No inventory yet.")));
        game.Runner.Start("t.n");
        game.Runner.Next();

        var exception = Assert.Throws<FastForwardException>(game.Runner.FastForward);

        Assert.Equal(["Command @show(rex, calm)"], exception.Passed.Select(Game.Format));
        Assert.IsType<HostFunctionException>(exception.InnerException);
        Assert.Equal(1m, game.State.GetVariable("count"));
        Assert.Empty(Assert.Throws<FastForwardException>(game.Runner.FastForward).Passed);
    }

    private static Game SaveAtFirstLine(string before, string after, out IReadOnlyList<SaveProblem> problems) => SaveAt<LineStep>(before, after, out problems);

    private static Game SaveAtFirstChoice(string before, string after, out IReadOnlyList<SaveProblem> problems) => SaveAt<ChoiceStep>(before, after, out problems);

    /// <summary>Plays <c>t.n</c> of <paramref name="before"/> to its first step of type <typeparamref name="T"/>, saves, and loads into <paramref name="after"/>.</summary>
    private static Game SaveAt<T>(string before, string after, out IReadOnlyList<SaveProblem> problems)
        where T : DialogueStep
    {
        Game game = Game.Of(before);
        game.Runner.Start("t.n");
        while (game.Runner.Next() is not T)
        {
        }

        return game.SaveAndLoad(out problems, Game.Of(after).Story);
    }

    private static (SaveProblemKind, string)[] Kinds(IEnumerable<SaveProblem> problems) => [.. problems.Select(problem => (problem.Kind, problem.Subject))];
}
