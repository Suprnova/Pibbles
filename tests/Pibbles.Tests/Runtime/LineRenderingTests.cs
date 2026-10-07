using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Tests.Runtime;

public class LineRenderingTests
{
    private enum Mood
    {
        calm,
        tense,
    }

    [Fact]
    public void Spans_NestAndAreOrderedByStartWithOuterFirst()
    {
        Line line = Render("mira: A [b]bold [i]both[/i][/b] [wave amplitude=2]w[/wave][b][i]x[/i][/b]");

        Assert.Equal("A bold both wx", line.Text);
        Assert.Equal(
            [("b", 2, 9), ("i", 7, 4), ("wave", 12, 1), ("b", 13, 1), ("i", 13, 1)],
            line.Spans.Select(span => (span.Name, span.Start, span.Length)));
    }

    [Fact]
    public void Spans_ReadTheirArgumentsByNameWithDefaultsFilledIn()
    {
        Line line = Render("mira: [wave amplitude=2]w[/wave][wave 3 4]x[/wave][color \"#ff0000\"]y[/color]");

        Assert.Equal((2m, 5m), (line.Spans[0].Arguments.GetNumber("amplitude"), line.Spans[0].Arguments.GetNumber("frequency")));
        Assert.Equal((3m, 4m), (line.Spans[1].Arguments.GetNumber("amplitude"), line.Spans[1].Arguments.GetNumber("frequency")));
        Assert.Equal("#ff0000", line.Spans[2].Arguments.GetString("value"));
        Assert.Throws<ArgumentException>(() => line.Spans[0].Arguments.GetNumber("speed"));
        Assert.Throws<InvalidOperationException>(() => line.Spans[0].Arguments.GetString("amplitude"));
    }

    [Fact]
    public void Spans_EmptySpanIsKept()
    {
        Line line = Render("mira: a[b][/b]c");

        Assert.Equal([("b", 1, 0)], line.Spans.Select(span => (span.Name, span.Start, span.Length)));
    }

    [Fact]
    public void Spans_OnlyTheChosenBranchOfConditionalTextAppears()
    {
        Line line = Render("mira: {if $key}[b]yes[/b]{w}{else}[i]no[/i]{/if}");

        Assert.Equal([("i", 0, 2)], line.Spans.Select(span => (span.Name, span.Start, span.Length)));
        Assert.Empty(line.Markers);
    }

    [Fact]
    public void Markers_AreAtTheirPositionsWithTheirPayloadsInSourceOrder()
    {
        Line line = Render("mira: {@shake 3}Hi{w} there{w 0.5}{p}new{speed 2}fast{speed}.{@shake 1 wait}");

        Assert.Equal("Hi therenewfast.", line.Text);
        Assert.Equal(
            [
                new CommandMarker(0, ((CommandMarker)line.Markers[0]).Command, false),
                new InputWaitMarker(2),
                new PauseMarker(8, TimeSpan.FromSeconds(0.5)),
                new PageBreakMarker(8),
                new SpeedMarker(11, 2),
                new SpeedMarker(15, 1),
                new CommandMarker(16, ((CommandMarker)line.Markers[6]).Command, true),
            ],
            line.Markers);
        Assert.Equal(3m, ((CommandMarker)line.Markers[0]).Command.GetNumber("strength"));
        Assert.Equal(1m, ((CommandMarker)line.Markers[6]).Command.GetNumber("strength"));
    }

    [Fact]
    public void Markers_SpeedStartsAtOneOnEveryLineWithoutAMarker()
    {
        Line line = Render("mira: {speed 3}fast");
        Line next = Render("mira: normal");

        Assert.Equal([new SpeedMarker(0, 3)], line.Markers);
        Assert.Empty(next.Markers);
    }

    [Fact]
    public void Markers_NonPositiveValuesAreLeftOutWithAWarning()
    {
        Game game = Game.Of("== t.n\nmira: a{w $delay}b{speed $count}c{w 0.5}\n");

        game.Runner.Start("t.n");
        var line = ((LineStep)game.Runner.Next()).Line;

        Assert.Equal([new PauseMarker(3, TimeSpan.FromSeconds(0.5))], line.Markers);
        Assert.Equal([RuntimeWarningKind.NonPositivePause, RuntimeWarningKind.NonPositiveSpeed], game.Warnings.Select(warning => warning.Kind));
        Assert.All(game.Warnings, warning => Assert.Equal("story.pib", warning.Location.Path));
    }

    [Fact]
    public void Icons_AreSeparateFromMarkersAndMatchTheirCharacters()
    {
        Line line = Render("mira: Press {icon bag} and [b]{icon bag}[/b]!{w}");

        Assert.Equal("Press ￼ and ￼!", line.Text);
        Assert.Equal([new Icon(6, "bag"), new Icon(12, "bag")], line.Icons);
        Assert.Equal(line.Icons.Select(icon => icon.Position), line.Text.Select((character, index) => (character, index)).Where(pair => pair.character == '￼').Select(pair => pair.index));
        Assert.Equal([("b", 12, 1)], line.Spans.Select(span => (span.Name, span.Start, span.Length)));
        Assert.Equal([new InputWaitMarker(14)], line.Markers);
    }

    [Fact]
    public void Positions_AreUtf16IndicesNotGraphemes()
    {
        Line line = Render("mira: 😀 {w}é{w}[b]x[/b]");

        Assert.Equal("😀 éx", line.Text);
        Assert.Equal([new InputWaitMarker(3), new InputWaitMarker(5)], line.Markers);
        Assert.Equal([("b", 5, 1)], line.Spans.Select(span => (span.Name, span.Start, span.Length)));
    }

    [Fact]
    public void Trimming_RenderedWhitespaceIsTrimmedAndPositionsMoveToTheNearestEnd()
    {
        Game game = Game.Of("== t.n\nmira: {$name}{w}x{br}{w}\nmira: [b]{$name}[/b]y{$name}{p}\nmira: {if $key}Ah, {/if}there.\nmira: A {$price}{br}\n");
        game.Set("name", Value.String("  "));

        game.Runner.Start("t.n");
        Line first = ((LineStep)game.Runner.Next()).Line;
        Line second = ((LineStep)game.Runner.Next()).Line;
        Line third = ((LineStep)game.Runner.Next()).Line;
        Line fourth = ((LineStep)game.Runner.Next()).Line;

        Assert.Equal("x", first.Text);
        Assert.Equal([new InputWaitMarker(0), new InputWaitMarker(1)], first.Markers);
        Assert.Equal("y", second.Text);
        Assert.Equal([("b", 0, 0)], second.Spans.Select(span => (span.Name, span.Start, span.Length)));
        Assert.Equal([new PageBreakMarker(1)], second.Markers);
        Assert.Equal("there.", third.Text);
        Assert.Equal("A 1.5", fourth.Text);
    }

    [Fact]
    public void Trimming_InterpolatedValuesKeepTheirInnerSpaces()
    {
        Game game = Game.Of("== t.n\nmira: A {$name}B\nmira: {if $key}x{/if}{$name}\n");
        game.Set("name", Value.String(" Sam "));

        game.Runner.Start("t.n");
        Line inner = ((LineStep)game.Runner.Next()).Line;
        Line edge = ((LineStep)game.Runner.Next()).Line;

        Assert.Equal("A  Sam B", inner.Text);
        Assert.Equal("Sam", edge.Text);
    }

    [Fact]
    public void ShownValues_ReplaceTheIconCharacterAndAreNeverReadAsMarkup()
    {
        Game game = Game.Of("== t.n\nmira: {$name}\n");
        game.Set("name", Value.String("a￼b [b]c[/b] {w}"));

        game.Runner.Start("t.n");
        Line line = ((LineStep)game.Runner.Next()).Line;

        Assert.Equal("a�b [b]c[/b] {w}", line.Text);
        Assert.Empty(line.Spans);
        Assert.Empty(line.Icons);
    }

    [Fact]
    public void Tags_ReachTheHostInSourceOrderWithTheirKindsAndTypedAccess()
    {
        Line line = Render("mira: Hi. #thought #box:tense #cue:rex_01 #id:abc123");

        Assert.Equal(
            [new Tag("thought", TagKind.Flag, null), new Tag("box", TagKind.Enum, "tense"), new Tag("cue", TagKind.Text, "rex_01"), new Tag("id", TagKind.Reserved, "abc123")],
            line.Tags);
        Assert.True(line.Tags.Has("thought"));
        Assert.Equal("tense", line.Tags.GetEnum("box"));
        Assert.Equal(Mood.tense, line.Tags.GetEnum<Mood>("box"));
        Assert.Equal("rex_01", line.Tags.GetString("cue"));
        Assert.Equal("abc123", line.Tags.GetString("id"));
        Assert.Equal("abc123", line.Id);
    }

    [Fact]
    public void Tags_DeclaredTagTheLineLacksIsNotMisuse()
    {
        Line line = Render("mira: Hi.");

        Assert.False(line.Tags.Has("thought"));
        Assert.Null(line.Tags.GetString("cue"));
        Assert.Null(line.Tags.GetEnum("box"));
        Assert.Null(line.Tags.GetEnum<Mood>("box"));
    }

    [Fact]
    public void Tags_Misuse_Throws()
    {
        Line line = Render("mira: Hi. #thought #box:tense");

        Assert.Throws<ArgumentException>(() => line.Tags.Has("undeclared"));
        Assert.Throws<InvalidOperationException>(() => line.Tags.GetString("box"));
        Assert.Throws<InvalidOperationException>(() => line.Tags.GetEnum("cue"));
        Assert.Throws<InvalidOperationException>(() => line.Tags.GetString("thought"));
    }

    [Fact]
    public void Options_HaveSpansIconsAndTagsAndNoMarkers()
    {
        Game game = Game.Of("== t.n\n-> Take the [b]bag[/b] {icon bag} #thought #id:o1\n    @end\n");
        game.Runner.Start("t.n");

        Line option = ((ChoiceStep)game.Runner.Next()).Options[0].Text;

        Assert.Equal("Take the bag ￼", option.Text);
        Assert.Equal([("b", 9, 3)], option.Spans.Select(span => (span.Name, span.Start, span.Length)));
        Assert.Equal([new Icon(13, "bag")], option.Icons);
        Assert.Empty(option.Markers);
        Assert.True(option.Tags.Has("thought"));
        Assert.Null(option.Speaker);
    }

    [Fact]
    public void Equality_LinesWithTheSameContentAreEqual()
    {
        Game game = Game.Of(
            "== t.n\nmira: Hi [b]there[/b]{w 1}{@shake 2} #thought #id:a1\n== t.m\nmira: Hi [b]there[/b]{w 2}{@shake 2} #thought #id:a2\n== t.k\nmira: Hi [b]there[/b]{w 1}{@shake 3} #thought #id:a3\n");
        Line Next(string node)
        {
            game.Runner.Start(node);
            return ((LineStep)game.Runner.Next()).Line;
        }

        Line a = Next("t.n");
        Line b = Next("t.n");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Next("t.m"));
        Assert.NotEqual(a, Next("t.k"));
    }

    [Fact]
    public void Rendering_HostFunctionThatFailsInASpanArgument_LeavesTheRunnerWhereItWas()
    {
        bool fail = true;
        Game game = Game.Of("== t.n\nmira: [wave amplitude=risky()]x[/wave]\n", register: functions => functions.Add("risky", () => fail ? throw new TimeoutException() : 2m));
        game.Runner.Start("t.n");

        Assert.Throws<HostFunctionException>(() => game.Runner.Next());
        fail = false;

        Assert.Equal(2m, ((LineStep)game.Runner.Next()).Line.Spans[0].Arguments.GetNumber("amplitude"));
    }

    [Fact]
    public void Story_ValidateEnum_ReportsMismatchesWithoutThrowing()
    {
        Story story = Game.Of("== t.n\nmira: x\n").Story;

        Assert.Empty(story.ValidateEnum<Mood>("mood"));
        Assert.Equal(
            [HostEnumProblemKind.MissingMember, HostEnumProblemKind.ExtraMember],
            story.ValidateEnum<DayOfWeek>("mood").Select(problem => problem.Kind).Distinct());
        Assert.Equal(HostEnumProblemKind.UnknownEnum, Assert.Single(story.ValidateEnum<Mood>("nope")).Kind);
    }

    [Fact]
    public void Story_Nodes_ListsEachNodeWithItsOldNames()
    {
        Story story = Game.Of("== t.new #was:t.old #was:t.older\nmira: x\n== t.other\nmira: y\n").Story;

        Assert.Equal(["t.new", "t.other"], story.Nodes.Select(node => node.Name));
        Assert.Equal(["t.old", "t.older"], story.Nodes[0].Aliases);
        Assert.Empty(story.Nodes[1].Aliases);
    }

    private static Line Render(string line)
    {
        Game game = Game.Of($"== t.n\n{line}\n");
        game.Runner.Start("t.n");
        return ((LineStep)game.Runner.Next()).Line;
    }
}
