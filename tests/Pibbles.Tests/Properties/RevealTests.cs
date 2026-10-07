using CsCheck;
using Pibbles.Runtime;
using Pibbles.Tests.Runtime;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Properties of <see cref="LineReveal"/> over random lines: skipping at any point fires the same effects, in the same
/// order, as revealing fully, and the same total time reaches the same place however it is split into frames.
/// </summary>
public class RevealTests
{
    /// <summary>Text pieces: plain characters, a line break, an icon, a combining mark, and emoji no newer than Unicode 15.</summary>
    private static readonly string[] Pieces = ["a", "b", "c", " ", "\n", "￼", "é", "\U0001F600", "\U0001F468‍\U0001F469‍\U0001F467", "\U0001F1EF\U0001F1F5"];

    private static readonly Gen<string> Text = Gen.OneOfConst(Pieces).Array[0, 8].Select(parts => string.Concat(parts));

    private static readonly Gen<Line> RandomLine = Text.SelectMany(text => MarkerAt(text.Length).Array[0, 6].Select(markers => LineRevealTests.Line(text, [.. markers.OrderBy(marker => marker.Position)])));

    /// <summary>What a caller does between frames: a number below 300 is milliseconds to advance, anything else is a skip.</summary>
    private static readonly Gen<int[]> Operations = Gen.Int[0, 340].Array[0, 10];

    private static Gen<Marker> MarkerAt(int length) => Gen.Select(
        Gen.Int[0, length],
        Gen.Int[0, 4],
        Gen.Int[1, 5],
        Gen.Bool,
        Gen.Int[1, 8],
        Marker (position, kind, id, waits, amount) => kind switch
        {
            0 => new InputWaitMarker(position),
            1 => new PauseMarker(position, TimeSpan.FromMilliseconds(amount * 100)),
            2 => new PageBreakMarker(position),
            3 => new SpeedMarker(position, amount / 2m),
            _ => LineRevealTests.Command(position, id, waits),
        });

    [Fact]
    public void Skip_AtAnyPoint_FiresTheSameEffectsAsRevealingFully() =>
        PropertyCheck.Run(Gen.Select(RandomLine, Operations, Gen.Bool, (line, operations, instant) => (Line: line, Operations: operations, Instant: instant)), Check, iterations: 3000, print: Print);

    [Fact]
    public void Advance_TheSameTotalTimeSplitDifferently_ReachesTheSamePlace() =>
        PropertyCheck.Run(Gen.Select(RandomLine, Gen.Int[1, 400].Array[1, 12], (line, deltas) => (Line: line, Deltas: deltas)), CheckSplit, iterations: 3000, print: pair => $"{Describe(pair.Line)} deltas {string.Join(',', pair.Deltas)}");

    private static void Check((Line Line, int[] Operations, bool Instant) input)
    {
        string[] full = Effects(input.Line, [], instant: false);
        string[] mixed = Effects(input.Line, input.Operations, input.Instant);

        Assert.Equal(full, mixed);
    }

    private static void CheckSplit((Line Line, int[] Deltas) input)
    {
        var whole = new LineReveal(input.Line, new RevealSettings(10));
        var parts = new LineReveal(input.Line, new RevealSettings(10));

        RevealFrame all = whole.Advance(TimeSpan.FromMilliseconds(input.Deltas.Sum()));
        List<Marker> fired = [];
        RevealFrame last = null!;
        foreach (int delta in input.Deltas)
        {
            last = parts.Advance(TimeSpan.FromMilliseconds(delta));
            fired.AddRange(last.Fired);
        }

        Assert.Equal(LineRevealTests.Show(all), LineRevealTests.Show(last with { Fired = all.Fired }));
        Assert.Equal(all.Fired.Select(LineRevealTests.Describe), fired.Select(LineRevealTests.Describe));
    }

    /// <summary>The commands fired, in order, by revealing with the operations and then to the end, resuming at every stop.</summary>
    private static string[] Effects(Line line, int[] operations, bool instant)
    {
        var reveal = new LineReveal(line, new RevealSettings(10, instant));
        List<string> effects = [];
        void Take(RevealFrame frame) => effects.AddRange(frame.Fired.OfType<CommandMarker>().Select(LineRevealTests.Describe));
        void Wake()
        {
            if (reveal.State is RevealState.WaitingForInput or RevealState.WaitingForHost)
                reveal.Resume();
        }

        foreach (int operation in operations)
        {
            Take(operation < 300 ? reveal.Advance(TimeSpan.FromMilliseconds(operation)) : reveal.Skip());
            Wake();
        }

        for (int guard = 0; reveal.State is not RevealState.Complete; guard++)
        {
            Assert.True(guard < 100, "The reveal never completed.");
            Take(reveal.Advance(TimeSpan.FromHours(1)));
            Wake();
        }

        return [.. effects];
    }

    private static string Print((Line Line, int[] Operations, bool Instant) input) =>
        $"{Describe(input.Line)} instant={input.Instant} operations {string.Join(',', input.Operations)}";

    private static string Describe(Line line) =>
        $"\"{line.Text.Replace("\n", "\\n", StringComparison.Ordinal)}\" markers {string.Join(' ', line.Markers.Select(LineRevealTests.Describe))}";
}
