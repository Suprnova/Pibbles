using System.Collections.Immutable;

namespace Pibbles.Runtime;

/// <summary>How a <see cref="LineReveal"/> shows a line.</summary>
/// <param name="CharactersPerSecond">The player's text speed, in characters per second. It must be more than zero. A <c>{speed x}</c> marker multiplies it from that point on.</param>
/// <param name="Instant">Whether to show everything up to the next stop at once, with no timing.</param>
public sealed record RevealSettings(decimal CharactersPerSecond, bool Instant = false);

/// <summary>Where a <see cref="LineReveal"/> is.</summary>
public enum RevealState
{
    /// <summary>Characters are appearing.</summary>
    Revealing,

    /// <summary>The reveal reached a <c>{w}</c> or a <c>{p}</c>, and waits for the player. Call <see cref="LineReveal.Resume"/> when they answer.</summary>
    WaitingForInput,

    /// <summary>The reveal reached an inline command that waits for the host, and waits until the host calls <see cref="LineReveal.Resume"/>.</summary>
    WaitingForHost,

    /// <summary>Everything is shown, and every marker has fired.</summary>
    Complete,
}

/// <summary>What a <see cref="LineReveal"/> shows after a call, and what happened on the way.</summary>
/// <remarks>The text on screen is <c>line.Text[PageStart..VisibleLength]</c>; <see cref="Line.Text"/> itself is always the whole line.</remarks>
/// <param name="State">Where the reveal is.</param>
/// <param name="PageStart">Where the current page starts. It moves to a <c>{p}</c> marker's position when the reveal resumes after it, and the host shows only the text from there on.</param>
/// <param name="VisibleLength">How much of the line is visible: a UTF-16 index into the line's text, always on a grapheme cluster boundary.</param>
/// <param name="Fired">The markers the reveal reached during this call, in order.</param>
public sealed record RevealFrame(RevealState State, int PageStart, int VisibleLength, ImmutableArray<Marker> Fired)
{
    /// <summary>Frames are equal when all their fields are, including the markers.</summary>
    public bool Equals(RevealFrame? other) =>
        other is not null && State == other.State && PageStart == other.PageStart && VisibleLength == other.VisibleLength && Fired.SequenceEqual(other.Fired);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(State, PageStart, VisibleLength, Fired.Length);
}

/// <summary>
/// Shows a line one grapheme cluster at a time and reaches its markers, with no engine involved, so every host reveals
/// text the same way. It is optional: a host can read a line's markers itself.
/// </summary>
/// <remarks>
/// <para>
/// The reveal is a pure function of the line, the settings and the sequence of calls. Time is counted exactly, in ticks, so
/// the same total time reaches the same place however it is split into frames. Time that arrives while the reveal
/// waits, or after it has stopped at a marker, is not kept.
/// </para>
/// <para>
/// Each cluster takes <c>1 / (CharactersPerSecond × factor)</c> seconds, where the factor is 1 until a
/// <see cref="SpeedMarker"/> sets it. A line break takes no time, an icon takes one character's time, and a
/// <see cref="PauseMarker"/> holds the reveal for its duration. There are no extra pauses after punctuation.
/// </para>
/// <para>
/// A marker is reached when the reveal gets to its position, before the character there shows, and markers at one position
/// keep their order. Markers at position 0 are reached on the first call, and markers at the end before the reveal can be
/// <see cref="RevealState.Complete"/>. A marker inside a grapheme cluster is reached just before the cluster shows.
/// In normal play <see cref="RevealFrame.Fired"/> holds every marker reached, including pauses and speed changes. While
/// skipping or in <see cref="RevealSettings.Instant"/> mode it holds the others, the ones with an effect or a stop:
/// commands, input waits and page breaks. Skipping applies speed changes without reporting them, so the speed is right
/// when timing resumes.
/// </para>
/// <para>
/// Command markers are effects, which the host carries out. A command that doesn't wait fires and the reveal carries on. One
/// that waits fires and the reveal waits for the host, in <see cref="RevealState.WaitingForHost"/>, until
/// <see cref="Resume"/>.
/// </para>
/// </remarks>
public sealed class LineReveal
{
    private const decimal TicksPerSecond = 10_000_000m;

    private readonly Line line;
    private readonly RevealSettings settings;
    private int markerIndex;
    private int position;
    private int pendingPageStart = -1;
    private decimal factor = 1;
    private decimal budget;
    private decimal pendingPause;

    /// <summary>Starts revealing a line from its first character.</summary>
    /// <param name="line">The line to reveal.</param>
    /// <param name="settings">How to reveal it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="RevealSettings.CharactersPerSecond"/> isn't more than zero.</exception>
    public LineReveal(Line line, RevealSettings settings)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(settings.CharactersPerSecond, 0m, nameof(settings));
        this.line = line;
        this.settings = settings;
    }

    /// <summary>Where the reveal is.</summary>
    public RevealState State { get; private set; } = RevealState.Revealing;

    /// <summary>Where the current page starts.</summary>
    public int PageStart { get; private set; }

    /// <summary>How much of the line is visible, as a UTF-16 index on a grapheme cluster boundary.</summary>
    public int VisibleLength => position;

    /// <summary>
    /// Lets time pass. The reveal shows what the time allows, reaching markers on the way, and stops at an input wait, a page
    /// break, a waiting command or the end. In <see cref="RevealSettings.Instant"/> mode it shows everything up to the next
    /// stop whatever the time. While the reveal waits or is complete, the frame is unchanged and the time is dropped.
    /// </summary>
    /// <param name="delta">How much time has passed since the last call.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delta"/> is negative.</exception>
    public RevealFrame Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        if (State is not RevealState.Revealing)
            return Frame([]);

        budget += delta.Ticks;
        return Run(timed: !settings.Instant);
    }

    /// <summary>
    /// Jumps to the next stop point: an input wait, a page break, a waiting command, or the end. Every effect marker it
    /// passes fires, in order, and the timing is dropped, so skipping never changes what happens. While the reveal waits or
    /// is complete, nothing happens.
    /// </summary>
    public RevealFrame Skip() => State is RevealState.Revealing ? Run(timed: false) : Frame([]);

    /// <summary>
    /// Carries on after an input wait or a page break, once the player has answered, or after a waiting command, once the host
    /// has finished it. After a page break the page starts where the break was.
    /// </summary>
    /// <exception cref="InvalidOperationException">The reveal isn't waiting.</exception>
    public RevealFrame Resume()
    {
        if (State is not (RevealState.WaitingForInput or RevealState.WaitingForHost))
            throw new InvalidOperationException("The reveal isn't waiting for anything.");

        if (pendingPageStart >= 0)
            PageStart = pendingPageStart;

        pendingPageStart = -1;
        State = RevealState.Revealing;
        return Frame([]);
    }

    private RevealFrame Run(bool timed)
    {
        ImmutableArray<Marker>.Builder fired = ImmutableArray.CreateBuilder<Marker>();
        if (!timed)
            pendingPause = 0;

        while (true)
        {
            if (timed && pendingPause > 0)
            {
                decimal paid = Math.Min(budget, pendingPause);
                budget -= paid;
                pendingPause -= paid;
                if (pendingPause > 0)
                    return Frame(fired.ToImmutable());
            }

            int end = position < line.Text.Length ? position + ClusterLength(position) : line.Text.Length + 1;
            if (markerIndex < line.Markers.Length && line.Markers[markerIndex].Position < end)
            {
                if (Reach(line.Markers[markerIndex++], timed, fired))
                    return Frame(fired.ToImmutable());

                continue;
            }

            if (position >= line.Text.Length)
            {
                State = RevealState.Complete;
                budget = 0;
                return Frame(fired.ToImmutable());
            }

            if (timed)
            {
                decimal cost = line.Text[position] == '\n' ? 0 : ClusterTicks();
                if (budget < cost)
                    return Frame(fired.ToImmutable());

                budget -= cost;
            }

            position = end;
        }
    }

    /// <summary>Reaches a marker. Returns <see langword="true"/> if the reveal stops there.</summary>
    private bool Reach(Marker marker, bool timed, ImmutableArray<Marker>.Builder fired)
    {
        switch (marker)
        {
            case PauseMarker pause:
                if (timed)
                {
                    fired.Add(marker);
                    pendingPause = pause.Duration.Ticks;
                }

                return false;

            case SpeedMarker speed:
                factor = speed.Factor;
                if (timed)
                    fired.Add(marker);

                return false;

            case CommandMarker command:
                fired.Add(marker);
                return command.Waits && Stop(RevealState.WaitingForHost);

            case PageBreakMarker:
                fired.Add(marker);
                pendingPageStart = marker.Position;
                return Stop(RevealState.WaitingForInput);

            case InputWaitMarker:
                fired.Add(marker);
                return Stop(RevealState.WaitingForInput);

            default:
                fired.Add(marker);
                return false;
        }
    }

    private bool Stop(RevealState state)
    {
        State = state;
        budget = 0;
        return true;
    }

    /// <summary>The ticks one cluster takes at the current speed, kept inside what a decimal holds so no content can make it overflow.</summary>
    private decimal ClusterTicks()
    {
        try
        {
            decimal rate = settings.CharactersPerSecond * factor;
            return rate < 0.000000001m ? decimal.MaxValue : TicksPerSecond / rate;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    private int ClusterLength(int index) => System.Globalization.StringInfo.GetNextTextElementLength(line.Text.AsSpan(index));

    private RevealFrame Frame(ImmutableArray<Marker> fired) => new(State, PageStart, position, fired);
}
