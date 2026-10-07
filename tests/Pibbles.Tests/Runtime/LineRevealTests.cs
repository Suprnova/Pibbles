using System.Collections.Immutable;
using Pibbles.Runtime;
using Pibbles.Semantics;

namespace Pibbles.Tests.Runtime;

public class LineRevealTests
{
    private static readonly TimeSpan Character = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void EmptyLineWithOnlyAnInputWait_WaitsThenCompletes()
    {
        LineReveal reveal = Reveal("", new InputWaitMarker(0));

        Assert.Equal("WaitingForInput page=0 visible=0 fired=[w@0]", Show(reveal.Advance(TimeSpan.Zero)));
        Assert.Equal("WaitingForInput page=0 visible=0 fired=[]", Show(reveal.Advance(Character)));
        Assert.Equal("Revealing page=0 visible=0 fired=[]", Show(reveal.Resume()));
        Assert.Equal("Complete page=0 visible=0 fired=[]", Show(reveal.Advance(TimeSpan.Zero)));
    }

    [Fact]
    public void Markers_AtTheStartFireOnTheFirstAdvanceBeforeAnyCharacter()
    {
        LineReveal reveal = Reveal("ab", Command(0, 1));

        Assert.Equal("Revealing page=0 visible=0 fired=[cmd1@0]", Show(reveal.Advance(TimeSpan.Zero)));
        Assert.Equal("Revealing page=0 visible=1 fired=[]", Show(reveal.Advance(Character)));
    }

    [Fact]
    public void Markers_AtTheEndFireBeforeTheRevealIsComplete()
    {
        LineReveal reveal = Reveal("a", Command(1, 1));

        Assert.Equal("Revealing page=0 visible=0 fired=[]", Show(reveal.Advance(TimeSpan.Zero)));
        Assert.Equal("Complete page=0 visible=1 fired=[cmd1@1]", Show(reveal.Advance(Character)));
    }

    [Fact]
    public void Markers_SeveralAtOnePositionKeepTheirOrder()
    {
        LineReveal reveal = Reveal("ab", Command(1, 1), Command(1, 2), Command(1, 3));

        Assert.Equal("Complete page=0 visible=2 fired=[cmd1@1,cmd2@1,cmd3@1]", Show(reveal.Advance(TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void Characters_AppearAtTheSettingsRate()
    {
        LineReveal reveal = Reveal("abc");

        Assert.Equal("Revealing page=0 visible=0 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(99))));
        Assert.Equal("Revealing page=0 visible=1 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(1))));
        Assert.Equal("Complete page=0 visible=3 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(200))));
    }

    [Fact]
    public void PageBreak_StopsAndMovesThePageStartWhenTheRevealResumes()
    {
        LineReveal reveal = Reveal("abcd", new PageBreakMarker(2));

        Assert.Equal("WaitingForInput page=0 visible=2 fired=[p@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        Assert.Equal("Revealing page=2 visible=2 fired=[]", Show(reveal.Resume()));
        Assert.Equal("Complete page=2 visible=4 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void PageBreak_SpaceAfterBreak_PageStartMovesPastSpace()
    {
        LineReveal reveal = Reveal("ab cd", new PageBreakMarker(2));

        Assert.Equal("WaitingForInput page=0 visible=2 fired=[p@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        Assert.Equal("Revealing page=3 visible=3 fired=[]", Show(reveal.Resume()));
        Assert.Equal("Complete page=3 visible=5 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void PageBreak_LineBreakAfterBreak_PageStartMovesPastLineBreak()
    {
        LineReveal reveal = Reveal("ab\ncd", new PageBreakMarker(2));

        Assert.Equal("WaitingForInput page=0 visible=2 fired=[p@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        Assert.Equal("Revealing page=3 visible=3 fired=[]", Show(reveal.Resume()));
        Assert.Equal("Complete page=3 visible=5 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void PageBreak_MarkerRightAfterBreak_DoesNotMovePastMarker()
    {
        LineReveal reveal = Reveal("ab cd", new PageBreakMarker(2), Command(2, 1));

        Assert.Equal("WaitingForInput page=0 visible=2 fired=[p@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        Assert.Equal("Revealing page=2 visible=2 fired=[]", Show(reveal.Resume()));
        Assert.Equal("Complete page=2 visible=5 fired=[cmd1@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void InputWait_StopsWithoutMovingThePage()
    {
        LineReveal reveal = Reveal("abcd", new InputWaitMarker(2));

        Assert.Equal("WaitingForInput page=0 visible=2 fired=[w@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        reveal.Resume();
        Assert.Equal("Complete page=0 visible=4 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void Commands_BlockingOnesWaitForTheHostAndNonBlockingOnesDont()
    {
        LineReveal reveal = Reveal("abc", Command(1, 1), Command(2, 2, waits: true));

        Assert.Equal("WaitingForHost page=0 visible=2 fired=[cmd1@1,cmd2@2]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        Assert.Equal("WaitingForHost page=0 visible=2 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
        reveal.Resume();
        Assert.Equal("Complete page=0 visible=3 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void Speed_ChangesApplyFromTheirPositionAndReplaceEachOther()
    {
        LineReveal reveal = Reveal("abcd", new SpeedMarker(1, 2), new SpeedMarker(3, 1));

        Assert.Equal("Revealing page=0 visible=1 fired=[speed2@1]", Show(reveal.Advance(Character)));
        Assert.Equal("Revealing page=0 visible=3 fired=[speed1@3]", Show(reveal.Advance(Character)));
        Assert.Equal("Complete page=0 visible=4 fired=[]", Show(reveal.Advance(Character)));
    }

    [Fact]
    public void Speed_SlowerThanTheSettingTakesLonger()
    {
        LineReveal reveal = Reveal("ab", new SpeedMarker(0, 0.5m));

        Assert.Equal("Revealing page=0 visible=0 fired=[speed0.5@0]", Show(reveal.Advance(Character)));
        Assert.Equal("Revealing page=0 visible=1 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(100))));
    }

    [Fact]
    public void Pause_HoldsTheRevealForItsDuration()
    {
        LineReveal reveal = Reveal("ab", new PauseMarker(1, TimeSpan.FromMilliseconds(500)));

        Assert.Equal("Revealing page=0 visible=1 fired=[pause@1]", Show(reveal.Advance(Character)));
        Assert.Equal("Revealing page=0 visible=1 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(400))));
        Assert.Equal("Revealing page=0 visible=1 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(100))));
        Assert.Equal("Complete page=0 visible=2 fired=[]", Show(reveal.Advance(Character)));
    }

    [Fact]
    public void Pause_MarkersAfterItFireOnlyWhenItEnds()
    {
        LineReveal reveal = Reveal("a", new PauseMarker(1, TimeSpan.FromSeconds(1)), Command(1, 1));

        Assert.Equal("Revealing page=0 visible=1 fired=[pause@1]", Show(reveal.Advance(Character)));
        Assert.Equal("Complete page=0 visible=1 fired=[cmd1@1]", Show(reveal.Advance(TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void Instant_ShowsEverythingUpToTheNextStopDroppingTimingButFiringEffects()
    {
        LineReveal reveal = RevealInstant("ab cd", new PauseMarker(0, TimeSpan.FromSeconds(9)), new SpeedMarker(1, 0.1m), Command(1, 1), new InputWaitMarker(3), Command(4, 2));

        Assert.Equal("WaitingForInput page=0 visible=3 fired=[cmd1@1,w@3]", Show(reveal.Advance(TimeSpan.Zero)));
        reveal.Resume();
        Assert.Equal("Complete page=0 visible=5 fired=[cmd2@4]", Show(reveal.Advance(TimeSpan.Zero)));
    }

    [Fact]
    public void Skip_FiresEveryEffectItPassesDropsTimingAndStopsAtTheNextStop()
    {
        LineReveal reveal = Reveal("abcdef", new PauseMarker(1, TimeSpan.FromSeconds(9)), new SpeedMarker(2, 0.1m), Command(3, 1), new PageBreakMarker(4), Command(5, 2));

        Assert.Equal("Revealing page=0 visible=0 fired=[]", Show(reveal.Advance(TimeSpan.Zero)));
        Assert.Equal("WaitingForInput page=0 visible=4 fired=[cmd1@3,p@4]", Show(reveal.Skip()));
        Assert.Equal("WaitingForInput page=0 visible=4 fired=[]", Show(reveal.Skip()));
        reveal.Resume();
        Assert.Equal("Complete page=4 visible=6 fired=[cmd2@5]", Show(reveal.Skip()));
    }

    [Fact]
    public void Skip_AfterAPauseHasStarted_DropsTheRestOfThePause()
    {
        LineReveal reveal = Reveal("ab", new PauseMarker(1, TimeSpan.FromSeconds(9)));
        reveal.Advance(Character);

        Assert.Equal("Complete page=0 visible=2 fired=[]", Show(reveal.Skip()));
    }

    [Fact]
    public void Skip_StillAppliesSpeedChangesSoTimingResumesAtTheRightSpeed()
    {
        LineReveal reveal = Reveal("abc", new SpeedMarker(0, 2), new InputWaitMarker(1));
        reveal.Skip();
        reveal.Resume();

        Assert.Equal("Complete page=0 visible=3 fired=[]", Show(reveal.Advance(TimeSpan.FromMilliseconds(100))));
    }

    [Fact]
    public void Clusters_CombiningMarksAndEmojiNeverShowHalfDrawn()
    {
        string text = "éx\U0001F468‍\U0001F469‍\U0001F467\U0001F1EF\U0001F1F5";
        LineReveal reveal = Reveal(text);

        Assert.Equal(2, reveal.Advance(Character).VisibleLength);
        Assert.Equal(3, reveal.Advance(Character).VisibleLength);
        Assert.Equal(3 + 8, reveal.Advance(Character).VisibleLength);
        Assert.Equal(text.Length, reveal.Advance(Character).VisibleLength);
    }

    [Fact]
    public void Clusters_AMarkerInsideOneFiresJustBeforeItShows()
    {
        LineReveal reveal = Reveal("éx", Command(1, 1));

        Assert.Equal("Revealing page=0 visible=0 fired=[cmd1@1]", Show(reveal.Advance(TimeSpan.Zero)));
        Assert.Equal(2, reveal.Advance(Character).VisibleLength);
    }

    [Fact]
    public void Icon_TakesOneCharacterOfTime()
    {
        LineReveal reveal = Reveal("a￼b");

        Assert.Equal(2, reveal.Advance(TimeSpan.FromMilliseconds(200)).VisibleLength);
    }

    [Fact]
    public void LineBreak_TakesNoTime()
    {
        LineReveal reveal = Reveal("a\nb");

        Assert.Equal(2, reveal.Advance(Character).VisibleLength);
        Assert.Equal(3, reveal.Advance(Character).VisibleLength);
    }

    [Fact]
    public void Spaces_AreTimedLikeAnyOtherCharacter()
    {
        LineReveal reveal = Reveal("a b");

        Assert.Equal(2, reveal.Advance(TimeSpan.FromMilliseconds(200)).VisibleLength);
    }

    [Fact]
    public void Misuse_Throws()
    {
        LineReveal reveal = Reveal("a");

        Assert.Throws<InvalidOperationException>(() => reveal.Resume());
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Advance(TimeSpan.FromMilliseconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineReveal(Line("a"), new RevealSettings(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineReveal(Line("a"), new RevealSettings(-5, Instant: true)));
    }

    [Fact]
    public void Complete_StaysCompleteAndAdvanceChangesNothing()
    {
        LineReveal reveal = Reveal("a");
        reveal.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal("Complete page=0 visible=1 fired=[]", Show(reveal.Advance(TimeSpan.FromSeconds(1))));
        Assert.Equal("Complete page=0 visible=1 fired=[]", Show(reveal.Skip()));
        Assert.Throws<InvalidOperationException>(() => reveal.Resume());
    }

    [Fact]
    public void Reveal_AppliesToLinesRenderedFromTemplates()
    {
        Game game = Game.Of("== t.n\nmira: Hi{w} [b]there[/b]{@shake 4 wait}!{p}Next.\n");
        game.Runner.Start("t.n");
        Line line = ((LineStep)game.Runner.Next()).Line;
        var reveal = new LineReveal(line, new RevealSettings(1000));

        RevealFrame first = reveal.Advance(TimeSpan.FromSeconds(1));
        reveal.Resume();
        RevealFrame second = reveal.Advance(TimeSpan.FromSeconds(1));
        reveal.Resume();
        RevealFrame third = reveal.Advance(TimeSpan.FromSeconds(1));
        reveal.Resume();
        RevealFrame fourth = reveal.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal([RevealState.WaitingForInput, RevealState.WaitingForHost, RevealState.WaitingForInput, RevealState.Complete], [first.State, second.State, third.State, fourth.State]);
        Assert.Equal(line.Text.Length, fourth.VisibleLength);
        Assert.Equal(line.Text.IndexOf("Next", StringComparison.Ordinal), fourth.PageStart);
    }

    [Fact]
    public void Frames_CompareByValue()
    {
        LineReveal a = Reveal("ab", Command(1, 1));
        LineReveal b = Reveal("ab", Command(1, 1));

        Assert.Equal(a.Advance(TimeSpan.FromSeconds(1)), b.Advance(TimeSpan.FromSeconds(1)));
    }

    internal static Line Line(string text, params Marker[] markers) =>
        new("line", null, null, text, [], [.. markers], [], new TagCollection([], new Dictionary<string, TagSymbol>()));

    internal static LineReveal Reveal(string text, params Marker[] markers) => new(Line(text, markers), new RevealSettings(10));

    internal static LineReveal RevealInstant(string text, params Marker[] markers) => new(Line(text, markers), new RevealSettings(10, Instant: true));

    internal static CommandMarker Command(int position, int id, bool waits = false) => new(position, Commands[id - 1], waits);

    internal static string Show(RevealFrame frame) =>
        $"{frame.State} page={frame.PageStart} visible={frame.VisibleLength} fired=[{string.Join(',', frame.Fired.Select(Describe))}]";

    internal static string Describe(Marker marker) => marker switch
    {
        InputWaitMarker => $"w@{marker.Position}",
        PauseMarker => $"pause@{marker.Position}",
        PageBreakMarker => $"p@{marker.Position}",
        SpeedMarker speed => $"speed{speed.Factor}@{marker.Position}",
        CommandMarker command => $"cmd{command.Command.GetNumber("strength")}@{marker.Position}",
        _ => throw new NotSupportedException(marker.GetType().Name),
    };

    internal static ImmutableArray<CommandInvocation> Commands { get; } = MakeCommands();

    private static ImmutableArray<CommandInvocation> MakeCommands()
    {
        Game game = Game.Of("== t.n\n@shake 1\n@shake 2\n@shake 3\n@shake 4\n@shake 5\n");
        game.Runner.Start("t.n");
        return [.. Enumerable.Range(0, 5).Select(_ => ((CommandStep)game.Runner.Next()).Command)];
    }
}
