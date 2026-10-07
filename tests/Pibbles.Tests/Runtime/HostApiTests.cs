using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Tests.Runtime;

/// <summary>The public API a host uses to read the story's metadata, exchange values with it, and register functions at run time.</summary>
public class HostApiTests
{
    private const string Source = """
        @actor mira
            name: Mira
            poses: neutral, happy
        @actor rex
            name: Rex
        @enum mood: calm, tense
        @var $count = 0
        @var $price = 1.50
        @var $name = "Sam"
        @var $key = false
        @var $delay = 2s
        @var $mood: mood = calm
        @var $who: actor = rex
        @var $where: node = t.n
        @command wave(amount: number = 2, wait_for: duration = 1s, how: mood = calm, at: node = t.n) inline
        @markup shout(volume: number = 3)
        @function has_item(id: string) -> bool
        @function seen(at: node, by: actor) -> mood
        @function pause(n: number, d: duration) -> duration

        == t.n #was:t.old
        -> One #id:o1
            mira: x #id:l1
        -> Two  @once #id:o2
        -> Three
        {@wave}[shout]x[/shout] #id:l2

        == t.f
        @if has_item("key")
            mira: have #id:f1
        @wait pause(1, 0.5s)
        mira: {if seen(t.n, rex) == tense}tense{else}calm{/if} #id:f2
        """;

    private static Game Make(Action<HostFunctions>? register = null) => Game.Of(Source, register ?? (_ => { }), declarations: false);

    [Fact]
    public void Story_Metadata_ListsFunctionsVariablesActorsAndEnums()
    {
        Story story = Make().Story;

        Assert.Equal(["has_item", "seen", "pause"], story.Functions.Select(function => function.Name));
        FunctionInfo seen = story.Functions[1];
        Assert.Equal(
            [("at", StoryTypeKind.Node), ("by", StoryTypeKind.Actor)],
            seen.Parameters.Select(parameter => (parameter.Name, parameter.Type.Kind)));
        Assert.Equal(new StoryType(StoryTypeKind.Enum, "mood"), seen.ReturnType);
        Assert.Equal(["count", "price", "name", "key", "delay", "mood", "who", "where"], story.Variables.Select(variable => variable.Name));
        Assert.Equal(
            [StoryTypeKind.Number, StoryTypeKind.Number, StoryTypeKind.Text, StoryTypeKind.Bool, StoryTypeKind.Duration, StoryTypeKind.Enum, StoryTypeKind.Actor, StoryTypeKind.Node],
            story.Variables.Select(variable => variable.Type.Kind));
        ActorInfo mira = story.Actors.Single(actor => actor.Id == "mira");
        Assert.Equal("Mira", mira.DisplayName);
        Assert.Equal(["neutral", "happy"], mira.Poses);
        Assert.Equal("Rex", story.Actors.Single(actor => actor.Id == "rex").DisplayName);
        Assert.Equal(["calm", "tense"], Assert.Single(story.Enums).Members);
    }

    [Theory]
    [InlineData(StoryTypeKind.Bool, typeof(bool))]
    [InlineData(StoryTypeKind.Number, typeof(decimal))]
    [InlineData(StoryTypeKind.Text, typeof(string))]
    [InlineData(StoryTypeKind.Duration, typeof(TimeSpan))]
    [InlineData(StoryTypeKind.Enum, typeof(string))]
    [InlineData(StoryTypeKind.Actor, typeof(string))]
    [InlineData(StoryTypeKind.Node, typeof(string))]
    public void StoryType_HostType_IsTheOneMapping(StoryTypeKind kind, Type expected) =>
        Assert.Equal(expected, new StoryType(kind, kind is StoryTypeKind.Enum ? "mood" : null).HostType);

    [Fact]
    public void StoryType_ToString_WritesTheTypeAsTheStoryDoes()
    {
        Assert.Equal("string", new StoryType(StoryTypeKind.Text).ToString());
        Assert.Equal("duration", new StoryType(StoryTypeKind.Duration).ToString());
        Assert.Equal("mood", new StoryType(StoryTypeKind.Enum, "mood").ToString());
    }

    [Fact]
    public void GetVariable_GivesEachTypeAsItsHostValue()
    {
        StoryState state = Make().State;

        Assert.Equal(0m, state.GetVariable("count"));
        Assert.Equal(1.5m, state.GetVariable("price"));
        Assert.Equal("Sam", state.GetVariable("name"));
        Assert.Equal(false, state.GetVariable("key"));
        Assert.Equal(TimeSpan.FromSeconds(2), state.GetVariable("delay"));
        Assert.Equal("calm", state.GetVariable("mood"));
        Assert.Equal("rex", state.GetVariable("who"));
        Assert.Equal("t.n", state.GetVariable("where"));
    }

    [Fact]
    public void SetVariable_TakesHostValuesAndChecksThemAgainstTheType()
    {
        StoryState state = Make().State;

        state.SetVariable("count", 7m);
        state.SetVariable("name", "Alex");
        state.SetVariable("key", true);
        state.SetVariable("delay", TimeSpan.FromMilliseconds(250));
        state.SetVariable("mood", "tense");
        state.SetVariable("who", "mira");
        state.SetVariable("where", "t.old");

        Assert.Equal(7m, state.GetVariable("count"));
        Assert.Equal("Alex", state.GetVariable("name"));
        Assert.Equal(true, state.GetVariable("key"));
        Assert.Equal(TimeSpan.FromMilliseconds(250), state.GetVariable("delay"));
        Assert.Equal("tense", state.GetVariable("mood"));
        Assert.Equal("mira", state.GetVariable("who"));
        Assert.Equal("t.n", state.GetVariable("where"));
    }

    [Fact]
    public void SetVariable_Misuse_ThrowsArgumentException()
    {
        StoryState state = Make().State;

        Assert.Throws<ArgumentException>(() => state.GetVariable("missing"));
        Assert.Throws<ArgumentException>(() => state.SetVariable("missing", 1m));
        Assert.Throws<ArgumentException>(() => state.SetVariable("count", "7"));
        Assert.Throws<ArgumentException>(() => state.SetVariable("count", 7));
        Assert.Throws<ArgumentException>(() => state.SetVariable("count", null!));
        Assert.Throws<ArgumentException>(() => state.SetVariable("mood", "furious"));
        Assert.Throws<ArgumentException>(() => state.SetVariable("who", "nobody"));
        Assert.Throws<ArgumentException>(() => state.SetVariable("where", "t.nowhere"));
        Assert.Equal(0m, state.GetVariable("count"));
    }

    [Fact]
    public void SetVariable_ChangesWhatTheStoryReads()
    {
        Game game = Make();
        game.State.SetVariable("count", 3m);

        Assert.Equal(3m, game.State.GetVariable("count"));
    }

    [Fact]
    public void AddDynamic_RegistersAFunctionOverHostValuesAndCallsItAtRunTime()
    {
        List<object?[]> calls = [];
        HostFunctions functions = new HostFunctions()
            .AddDynamic("has_item", [typeof(string)], typeof(bool), arguments => arguments[0] is "key")
            .AddDynamic("seen", [typeof(string), typeof(string)], typeof(string), arguments =>
            {
                calls.Add(arguments);
                return "tense";
            })
            .AddDynamic("pause", [typeof(decimal), typeof(TimeSpan)], typeof(TimeSpan), arguments => (TimeSpan)arguments[1]!);
        Game game = Make();
        var runner = new DialogueRunner(game.Story, game.State, functions);

        Assert.Empty(functions.Validate(game.Story));
        runner.Start("t.f");
        string[] steps = [Game.Format(runner.Next()), Game.Format(runner.Next()), Game.Format(runner.Next()), Game.Format(runner.Next())];

        Assert.Equal(["Line mira: have", "Wait 0.5s", "Line mira: tense", "End"], steps);
        Assert.Equal(["t.n", "rex"], Assert.Single(calls));
    }

    [Fact]
    public void AddDynamic_IsValidatedLikeTheTypedOverloads()
    {
        HostFunctions functions = new HostFunctions().AddDynamic("f", [typeof(string)], typeof(bool), _ => true);

        Assert.Throws<ArgumentException>(() => functions.AddDynamic("f", [], typeof(bool), _ => true));
        Assert.Throws<ArgumentException>(() => functions.AddDynamic("visits", [], typeof(bool), _ => true));
        Assert.Throws<ArgumentException>(() => functions.AddDynamic("g", [typeof(int)], typeof(bool), _ => true));
        Assert.Throws<ArgumentException>(() => functions.AddDynamic("g", [], typeof(double), _ => 1.0));
        Assert.ThrowsAny<ArgumentException>(() => functions.AddDynamic("g", [], typeof(bool), null!));
        Assert.ThrowsAny<ArgumentException>(() => functions.AddDynamic("g", null!, typeof(bool), _ => true));
    }

    [Fact]
    public void AddDynamic_SignatureThatDoesntMatchTheStory_IsReportedByValidate()
    {
        Game game = Make();
        HostFunctions functions = new HostFunctions()
            .AddDynamic("has_item", [typeof(decimal)], typeof(bool), _ => true)
            .AddDynamic("seen", [typeof(string), typeof(string)], typeof(string), _ => "calm")
            .AddDynamic("pause", [typeof(decimal), typeof(TimeSpan)], typeof(TimeSpan), _ => TimeSpan.Zero);

        HostFunctionProblem problem = Assert.Single(functions.Validate(game.Story));

        Assert.Equal((HostFunctionProblemKind.WrongSignature, "has_item"), (problem.Kind, problem.Function));
    }

    [Fact]
    public void AddDynamic_ResultOfTheWrongTypeOrNamingNothing_IsAHostError()
    {
        object? result = 1m;
        HostFunctions functions = new HostFunctions().AddDynamic("has_item", [typeof(string)], typeof(bool), _ => result);
        Game game = Game.Of(Source + "\n== t.k\n@if has_item(\"x\")\n    mira: yes #id:k1\n", register: _ => { }, declarations: false);
        var runner = new DialogueRunner(game.Story, game.State, functions);
        runner.Start("t.k");

        var wrongType = Assert.Throws<HostFunctionException>(() => runner.Next());
        result = null;
        var missing = Assert.Throws<HostFunctionException>(() => runner.Next());

        Assert.IsType<InvalidOperationException>(wrongType.InnerException);
        Assert.IsType<InvalidOperationException>(missing.InnerException);
    }

    [Fact]
    public void Arguments_ListTheirParametersAndGiveEachValueAsAHostValue()
    {
        Game game = Make();
        game.Runner.Start("t.n");
        game.Runner.Choose(((ChoiceStep)game.Runner.Next()).Options[0]);
        game.Runner.Next();
        var line = (LineStep)game.Runner.Next();
        CommandInvocation wave = ((CommandMarker)line.Line.Markers[0]).Command;
        Arguments shout = line.Line.Spans[0].Arguments;

        Assert.Equal(
            [("amount", StoryTypeKind.Number), ("wait_for", StoryTypeKind.Duration), ("how", StoryTypeKind.Enum), ("at", StoryTypeKind.Node)],
            wave.Parameters.Select(parameter => (parameter.Name, parameter.Type.Kind)));
        Assert.Equal(
            [2m, TimeSpan.FromSeconds(1), "calm", "t.n"],
            wave.Parameters.Select(parameter => wave.GetValue(parameter.Name)));
        Assert.Equal([("volume", StoryTypeKind.Number)], shout.Parameters.Select(parameter => (parameter.Name, parameter.Type.Kind)));
        Assert.Equal(3m, shout.GetValue("volume"));
        Assert.Throws<ArgumentException>(() => shout.GetValue("loudness"));
    }

    [Fact]
    public void Line_IsFallbackId_SaysWhetherTheIdWasMadeUp()
    {
        Game game = Game.Of("== t.n\nmira: Written. #id:l1\nmira: Not written.\n", declarations: true);

        game.Runner.Start("t.n");
        Line written = ((LineStep)game.Runner.Next()).Line;
        Line made = ((LineStep)game.Runner.Next()).Line;

        Assert.False(written.IsFallbackId);
        Assert.True(made.IsFallbackId);
        Assert.StartsWith("~", made.Id, StringComparison.Ordinal);
        Assert.NotEqual(written, made);
    }

    [Fact]
    public void ChoiceOption_Number_IsThePlaceAsWrittenEvenWhenOnceOptionsAreGone()
    {
        Game game = Make();

        game.Runner.Start("t.n");
        var first = (ChoiceStep)game.Runner.Next();
        game.Runner.Choose("o2");
        game.Runner.Start("t.n");
        var second = (ChoiceStep)game.Runner.Next();

        Assert.Equal([1, 2, 3], first.Options.Select(option => option.Number));
        Assert.Equal([1, 3], second.Options.Select(option => option.Number));
    }
}
