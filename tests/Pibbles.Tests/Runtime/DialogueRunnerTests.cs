using Pibbles.Runtime;

namespace Pibbles.Tests.Runtime;

public class DialogueRunnerTests
{
    [Fact]
    public void Next_LinesWithValuesAndLinesWithNoText_GiveTheirSteps()
    {
        Game game = Game.Of("== t.n\nNarration.\nmira: Hello.\nmira (happy, shake): Valued.\nmira (neutral):\nmira: {w}\n");

        string[] run = game.Play("t.n");

        Assert.Equal(["Line: Narration.", "Line mira: Hello.", "Line mira (pose=happy, effect=shake): Valued.", "Channel mira (pose=neutral)", "Line mira: ", "End"], run);
        Assert.Equal("neutral", game.State.GetChannel("mira", "pose"));
        Assert.Equal("shake", game.State.GetChannel("mira", "effect"));
    }

    [Fact]
    public void Values_Unchanged_AreStillOnEveryLine()
    {
        Game game = Game.Of("== t.n\nmira (happy): One.\nmira (happy): Two.\n");

        string[] run = game.Play("t.n");

        Assert.Equal(["Line mira (pose=happy): One.", "Line mira (pose=happy): Two.", "End"], run);
    }

    [Fact]
    public void GetChannel_BeforeAnyValueIsSet_IsTheActorsFirstValue()
    {
        Game game = Game.Of("== t.n\nmira: Hi.\n");

        Assert.Equal("neutral", game.State.GetChannel("mira", "pose"));
        Assert.Equal("still", game.State.GetChannel("rex", "effect"));
        Assert.Null(game.State.GetChannel("rex", "pose"));
    }

    [Fact]
    public void Next_LineWithValues_CarriesThemInTheOrderWritten()
    {
        Game game = Game.Of("== t.n\nmira (shake, happy): Hi. #id:l1\n");
        game.Runner.Start("t.n");

        var line = Assert.IsType<LineStep>(game.Runner.Next()).Line;

        Assert.Equal([new ChannelValue("effect", "shake"), new ChannelValue("pose", "happy")], line.Values);
    }

    [Fact]
    public void Next_ParenthesisWithNoTextAndATag_CarriesItsValuesAndTags()
    {
        Game game = Game.Of("@channel pose: neutral, happy\n@tag thought\n@actor mira\n    name: Mira\n\n== t.n\nmira (happy): #thought\nmira: Hi. #id:l1\n", declarations: false);
        game.Runner.Start("t.n");

        var step = Assert.IsType<ChannelStep>(game.Runner.Next());

        Assert.Equal([new ChannelValue("pose", "happy")], step.Values);
        Assert.Equal(("mira", true), (step.Actor, step.Tags.Has("thought")));
    }

    [Fact]
    public void Next_NarrationWithValues_CarriesThemAndTheNarrationName()
    {
        Game game = Game.Of("== t.n\n(shake): The floor rumbles. #id:l1\nNo values here. #id:l2\n(still):\n");
        game.Runner.Start("t.n");

        var line = Assert.IsType<LineStep>(game.Runner.Next()).Line;
        var plain = Assert.IsType<LineStep>(game.Runner.Next()).Line;
        var step = Assert.IsType<ChannelStep>(game.Runner.Next());

        Assert.Equal([new ChannelValue("effect", "shake")], line.Values);
        Assert.Null(line.Speaker);
        Assert.Null(step.Actor);
        Assert.Equal(["Narrator", "Narrator"], [line.SpeakerName, plain.SpeakerName]);
        Assert.Equal([new ChannelValue("effect", "still")], step.Values);
        Assert.Equal("still", game.State.GetChannel(null, "effect"));
    }

    [Fact]
    public void Next_NarrationWithNoNameBlock_HasNoName()
    {
        Game game = Game.Of("@channel effect: still, shake\n== t.n\nNarration. #id:l1\n", declarations: false);
        game.Runner.Start("t.n");

        var line = Assert.IsType<LineStep>(game.Runner.Next()).Line;

        Assert.Null(line.SpeakerName);
        Assert.Equal("still", game.State.GetChannel(null, "effect"));
    }

    [Fact]
    public void GetChannel_NarrationWithoutTheChannel_Throws()
    {
        Game game = Game.Of("== t.n\nNarration.\n");

        Assert.Throws<ArgumentException>(() => game.State.GetChannel(null, "pose"));
    }

    [Fact]
    public void GetChannel_UnknownActorOrChannel_Throws()
    {
        Game game = Game.Of("== t.n\nmira: Hi.\n");

        Assert.Throws<ArgumentException>(() => game.State.GetChannel("nobody", "pose"));
        Assert.Throws<ArgumentException>(() => game.State.GetChannel("mira", "camera"));
    }

    [Fact]
    public void Line_Text_ShowsValuesBranchesBreaksIconsAndMarkupAsPlainText()
    {
        Game game = Game.Of("""
            == t.n
            mira: {$name} has {$price} and {$count}; {$who} waits.
            mira: A [shout]loud[/shout] {if $key}yes{elif $count > 0}some{else}no{/if}word.{br}Second {icon bag}!
            mira: {br}Trimmed.{br}
            @set $name = "[b]x[/b]"
            mira: {$name}
            """);

        string[] run = game.Play("t.n");

        Assert.Equal(
            ["Line mira: Sam has 1.5 and 0; Rex waits.", "Line mira: A loud noword.\\nSecond <icon>!", "Line mira: Trimmed.", "Line mira: [b]x[/b]", "End"],
            run);
    }

    [Fact]
    public void Next_SetAndConditions_FollowTheBranches()
    {
        Game game = Game.Of("== t.n\n@set $count += 2\n@if $count > 5\n    mira: big\n@elif $count > 1\n    mira: medium\n@else\n    mira: small\nmira: Count {$count}.\n");

        string[] run = game.Play("t.n");

        Assert.Equal(["Line mira: medium", "Line mira: Count 2.", "End"], run);
    }

    [Fact]
    public void Wait_NotMoreThanZero_IsSkippedWithAWarning()
    {
        Game game = Game.Of("== t.n\n@pause 0.5s\n@pause $count\n@pause $delay\n@pause 1\n");

        string[] run = game.Play("t.n");

        Assert.Equal(["Pause 0.5s", "Pause 1s", "End"], run);
        Assert.Equal([RuntimeWarningKind.NonPositivePause, RuntimeWarningKind.NonPositivePause], game.Warnings.Select(warning => warning.Kind));
        Assert.Equal(["story.pib", "story.pib"], game.Warnings.Select(warning => warning.Location.Path));
        Assert.True(game.Warnings[0].Location.Start.Line < game.Warnings[1].Location.Start.Line);
    }

    [Fact]
    public void Command_ArgumentsAreInParameterOrderWithDefaultsAndWaitOverrides()
    {
        Game game = Game.Of("== t.n\n@show mira\n@show rex tense\n@all true \"x\" 2.5 1.5s mira t.n\n@show mira wait\n");

        string[] run = game.Play("t.n");

        Assert.Equal(
            ["Command @show(mira, calm)", "Command @show(rex, tense)", "Command @all(true, \"x\", 2.5, 1.5s, mira, t.n, calm)", "Command @show(mira, calm) waits", "End"],
            run);
    }

    [Fact]
    public void CallReturnAndEnd_WorkAtDepth()
    {
        Game game = Game.Of("""
            == t.n
            mira: a
            @call t.sub
            mira: back
            == t.sub
            mira: in sub
            @call t.deep
            mira: sub end
            == t.deep
            mira: deep
            @return
            mira: never
            """);

        Assert.Equal(["Line mira: a", "Line mira: in sub", "Line mira: deep", "Line mira: sub end", "Line mira: back", "End"], game.Play("t.n"));
    }

    [Fact]
    public void Return_AtTheTopLevel_EndsTheDialogue()
    {
        Game game = Game.Of("== t.n\nmira: x\n@return\nmira: y\n");

        Assert.Equal(["Line mira: x", "End"], game.Play("t.n"));
    }

    [Fact]
    public void End_InsideACall_ClearsTheCallStack()
    {
        Game game = Game.Of("== t.n\n@call t.sub\nmira: after\n== t.sub\nmira: in\n@end\n");

        Assert.Equal(["Line mira: in", "End"], game.Play("t.n"));
    }

    [Fact]
    public void NodeEndingWithoutReturn_ReturnsToTheCallerOrEnds()
    {
        Game game = Game.Of("== t.n\n@call t.sub\nmira: after\n== t.sub\nmira: in\n");

        Assert.Equal(["Line mira: in", "Line mira: after", "End"], game.Play("t.n"));
    }

    [Fact]
    public void Visits_CountOnStartJumpAndCallButNotReturn()
    {
        Game game = Game.Of("""
            == t.a
            mira: a {visits(t.a)}/{visits(t.b)}
            @call t.b
            mira: back {visits(t.a)}/{visits(t.b)}
            @if visits(t.a) < 3
                @jump t.a
            == t.b
            @return
            """);

        string[] run = game.Play("t.a");

        Assert.Equal(
            ["Line mira: a 1/0", "Line mira: back 1/1", "Line mira: a 2/1", "Line mira: back 2/2", "Line mira: a 3/2", "Line mira: back 3/3", "End"],
            run);
    }

    [Fact]
    public void Start_AnOldName_FindsTheNodeAndCountsItUnderTheCurrentName()
    {
        Game game = Game.Of("== t.new #was:t.old\nmira: x\n");

        string[] run = game.Play("t.old");

        Assert.Equal(["Line mira: x", "End"], run);
        Assert.Equal(1, game.State.Visits["t.new"]);
    }

    [Fact]
    public void Start_UnknownNode_ThrowsSuggestingTheClosestName()
    {
        Game game = Game.Of("== t.next\nmira: x\n");

        var exception = Assert.Throws<InvalidOperationException>(() => game.Runner.Start("t.nxt"));

        Assert.Contains("`t.nxt`", exception.Message);
        Assert.Contains("Did you mean `t.next`?", exception.Message);
    }

    [Fact]
    public void Variations_PickByEntryCountForEachKind()
    {
        Game game = Game.Of("""
            == t.v
            @sequence
                - mira: s1
                - mira: s2
            @cycle
                - mira: c1
                - mira: c2
            @once
                mira: o1
            @if visits(t.v) < 4
                @jump t.v
            """);

        string[] run = game.Play("t.v");

        Assert.Equal(
            ["Line mira: s1", "Line mira: c1", "Line mira: o1", "Line mira: s2", "Line mira: c2", "Line mira: s2", "Line mira: c1", "Line mira: s2", "Line mira: c2", "End"],
            run);
    }

    [Fact]
    public void Variation_AlternativeThatJumps_StillRaisesItsCount()
    {
        Game game = Game.Of("== t.j\n@sequence #id:blk\n    - @jump t.other\n    - mira: second\n== t.other\nmira: other\n@jump t.j\n");

        string[] run = game.Play("t.j");

        Assert.Equal(["Line mira: other", "Line mira: second", "End"], run);
        Assert.Equal(2, game.State.BlockEntries["blk"]);
    }

    private const string Choices = """
        == t.c
        -> Sticky #id:o1
            mira: sticky body
        -> Once  @once #id:o2
            mira: once body
        -> Hidden  @if $key #id:o3
            mira: hidden body
        -> Empty #id:o4
        mira: after
        """;

    [Fact]
    public void Choice_OnceOptionsGoAfterBeingChosenAndStickyOnesStayMarkedChosen()
    {
        Game game = Game.Of(Choices);

        string[] first = game.Play("t.c", "Once");
        string[] second = game.Play("t.c", "Sticky");
        string[] third = game.Play("t.c", "Empty");

        Assert.Equal(["Choice Sticky | Once | Hidden (unavailable) | Empty", "Chose Once", "Line mira: once body", "Line mira: after", "End"], first);
        Assert.Equal(["Choice Sticky | Hidden (unavailable) | Empty", "Chose Sticky", "Line mira: sticky body", "Line mira: after", "End"], second);
        Assert.Equal(["Choice Sticky (chosen) | Hidden (unavailable) | Empty", "Chose Empty", "Line mira: after", "End"], third);
    }

    [Fact]
    public void Choice_UnavailableOptionIsDeliveredAndBecomesAvailableWhenItsConditionHolds()
    {
        Game game = Game.Of(Choices);
        game.Set("key", Value.Bool(true));

        string[] run = game.Play("t.c", "Hidden");

        Assert.Equal(["Choice Sticky | Once | Hidden | Empty", "Chose Hidden", "Line mira: hidden body", "Line mira: after", "End"], run);
    }

    [Fact]
    public void Choice_WithNoOptionAvailable_IsSkipped()
    {
        Game game = Game.Of("== t.n\n-> Nope  @if $key\n    mira: x\nmira: after\n");

        Assert.Equal(["Line mira: after", "End"], game.Play("t.n"));
    }

    [Fact]
    public void Choice_Nested_RunsInnerChoicesAndFallsThroughToTheirJoin()
    {
        string story = "== t.n\n-> Outer\n    -> Inner A\n        mira: A\n    -> Inner B\n        @end\nmira: done\n";

        Assert.Equal(["Choice Outer", "Chose Outer", "Choice Inner A | Inner B", "Chose Inner A", "Line mira: A", "Line mira: done", "End"], Game.Of(story).Play("t.n", "Outer", "Inner A"));
        Assert.Equal(["Choice Outer", "Chose Outer", "Choice Inner A | Inner B", "Chose Inner B", "End"], Game.Of(story).Play("t.n", "Outer", "Inner B"));
    }

    [Fact]
    public void Choose_ByIdOrByOption_RecordsTheOptionBeforeItsBodyRuns()
    {
        Game game = Game.Of(Choices);
        game.Runner.Start("t.c");
        var choice = (ChoiceStep)game.Runner.Next();

        game.Runner.Choose("o1");

        Assert.Contains("o1", game.State.ChosenOptions);
        Assert.Equal("Line mira: sticky body", Game.Format(game.Runner.Next()));
        Assert.Equal("o2", choice.Options[1].Id);
    }

    [Fact]
    public void Misuse_ThrowsInvalidOperationException()
    {
        Game game = Game.Of(Choices);
        DialogueRunner runner = game.Runner;

        Assert.Throws<InvalidOperationException>(() => runner.Next());
        Assert.Throws<InvalidOperationException>(() => runner.Choose("o1"));
        runner.Start("t.c");
        var choice = (ChoiceStep)runner.Next();
        Assert.Throws<InvalidOperationException>(() => runner.Next());
        Assert.Throws<InvalidOperationException>(() => runner.Choose("nope"));
        Assert.Throws<InvalidOperationException>(() => runner.Choose("o3"));
        Assert.Throws<InvalidOperationException>(() => runner.Choose(choice.Options[2]));
        Assert.Throws<InvalidOperationException>(() => runner.Start("t.missing"));
    }

    [Fact]
    public void Next_AfterEnd_KeepsReturningEndUntilStartIsCalledAgain()
    {
        Game game = Game.Of("== t.n\nmira: x\n");
        game.Play("t.n");

        Assert.IsType<EndStep>(game.Runner.Next());
        Assert.IsType<EndStep>(game.Runner.Next());
        game.Runner.Start("t.n");
        Assert.IsType<LineStep>(game.Runner.Next());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_InstructionBudgetThatIsNotPositive_Throws(int budget)
    {
        Game game = Game.Of("== t.n\nmira: x\n");

        Assert.Throws<ArgumentOutOfRangeException>(() => new DialogueRunner(game.Story, game.State, game.Functions, new() { InstructionBudget = budget }));
    }

    [Fact]
    public void Start_DropsTheDialogueInProgress()
    {
        Game game = Game.Of("== t.n\n@call t.sub\nmira: after\n== t.sub\nmira: in\n== t.other\nmira: other\n");
        game.Runner.Start("t.n");
        game.Runner.Next();

        Assert.Equal(["Line mira: other", "End"], game.Play("t.other"));
    }

    [Fact]
    public void Constructor_StateOfAnotherStory_Throws()
    {
        Game one = Game.Of("== t.n\nmira: x\n");
        Game other = Game.Of("== t.n\nmira: x\n");

        Assert.Throws<ArgumentException>(() => new DialogueRunner(one.Story, other.State, one.Functions));
    }

    [Fact]
    public void TwoRunnersSharingOneState_ShareVariablesAndVisitsButNotPosition()
    {
        Game game = Game.Of("== t.a\n@set $count += 1\nmira: a\n== t.b\nmira: b {$count} {visits(t.a)}\n");
        var second = new DialogueRunner(game.Story, game.State, game.Functions);

        game.Runner.Start("t.a");
        Assert.Equal("Line mira: a", Game.Format(game.Runner.Next()));
        second.Start("t.b");
        Assert.Equal("Line mira: b 1 1", Game.Format(second.Next()));
        Assert.IsType<EndStep>(game.Runner.Next());
        Assert.IsType<EndStep>(second.Next());
    }

    [Fact]
    public void Next_StoryThatLoopsForever_EndsWithAWarning()
    {
        const string Nodes = "== t.loop\n@jump t.loop\n";
        Game game = Game.Of(Nodes, budget: 50);

        string[] run = game.Play("t.loop");

        Assert.Equal(["End"], run);
        RuntimeWarning warning = Assert.Single(game.Warnings);
        Assert.Equal(RuntimeWarningKind.InfiniteLoop, warning.Kind);
        Assert.Equal(("story.pib", (Game.Defs + Nodes).Split('\n').ToList().IndexOf("== t.loop")), (warning.Location.Path, warning.Location.Start.Line));
        Assert.IsType<EndStep>(game.Runner.Next());
    }

    [Fact]
    public void Next_CountedLoopJustUnderTheBudget_Finishes()
    {
        const string Loop = "== t.n\n@set $count += 1\n@if $count < 30\n    @jump t.n\nmira: done\n";

        Assert.Equal(["Line mira: done", "End"], Game.Of(Loop, budget: 100).Play("t.n"));
        Game over = Game.Of(Loop, budget: 80);
        Assert.Equal(["End"], over.Play("t.n"));
        Assert.Equal(RuntimeWarningKind.InfiniteLoop, Assert.Single(over.Warnings).Kind);
    }

    [Fact]
    public void Next_InstructionThatThrows_ChangesNothingAndCanBeRetried()
    {
        bool fail = true;
        Game game = Game.Of("""
            == t.n
            @set $count += 1
            @if risky() > 0
                mira: ok
            @set $count += 10
            @shake risky()
            mira: Value {risky()}.
            -> Option {risky()}
                mira: x
            """, register: functions => functions.Add("risky", () => fail ? throw new TimeoutException() : 1m));
        game.Runner.Start("t.n");

        Assert.Throws<HostFunctionException>(() => game.Runner.Next());
        Assert.Equal(1m, game.Get("count").AsDecimal);
        fail = false;
        Assert.Equal("Line mira: ok", Game.Format(game.Runner.Next()));
        Assert.Equal(1m, game.Get("count").AsDecimal);

        fail = true;
        Assert.Throws<HostFunctionException>(() => game.Runner.Next());
        Assert.Equal(11m, game.Get("count").AsDecimal);
        fail = false;
        Assert.Equal("Command @shake(1, 0.3s)", Game.Format(game.Runner.Next()));

        fail = true;
        Assert.Throws<HostFunctionException>(() => game.Runner.Next());
        fail = false;
        Assert.Equal("Line mira: Value 1.", Game.Format(game.Runner.Next()));

        fail = true;
        Assert.Throws<HostFunctionException>(() => game.Runner.Next());
        fail = false;
        Assert.Equal("Choice Option 1", Game.Format(game.Runner.Next()));
    }

    [Fact]
    public void Warnings_RaisedByAnInstructionThatThrows_AreDropped()
    {
        bool fail = true;
        Game game = Game.Of("== t.n\n@pause 1 / $count + risky()\n", register: functions => functions.Add("risky", () => fail ? throw new TimeoutException() : 1m));
        game.Runner.Start("t.n");

        Assert.Throws<HostFunctionException>(() => game.Runner.Next());

        Assert.Empty(game.Warnings);
        fail = false;
        Assert.Equal("Pause 1s", Game.Format(game.Runner.Next()));
        Assert.Equal([RuntimeWarningKind.DivisionByZero], game.Warnings.Select(warning => warning.Kind));
    }
}
