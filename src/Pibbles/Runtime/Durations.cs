using Pibbles.Diagnostics;

namespace Pibbles.Runtime;

/// <summary>Converts between the runtime's durations, decimal seconds, and <see cref="TimeSpan"/> at the public API's edge.</summary>
internal static class Durations
{
    private const decimal TicksPerSecond = 10_000_000m;

    private static readonly decimal MaxSeconds = long.MaxValue / TicksPerSecond;

    private static readonly decimal MinSeconds = long.MinValue / TicksPerSecond;

    /// <summary>
    /// Rounds seconds to the nearest tick. A duration outside <see cref="TimeSpan"/>'s range clamps to its limit, with an
    /// <see cref="RuntimeWarningKind.Overflow"/> warning at <paramref name="location"/>.
    /// </summary>
    public static TimeSpan ToTimeSpan(decimal seconds, SourceLocation location, Action<RuntimeWarning> warn)
    {
        if (seconds > MaxSeconds || seconds < MinSeconds)
        {
            warn(new(RuntimeWarningKind.Overflow, "This duration is too long for the game to use, so I used the longest one it can.", location));
            return seconds < 0 ? TimeSpan.MinValue : TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks((long)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero));
    }

    /// <summary>The exact number of seconds in a <see cref="TimeSpan"/>.</summary>
    public static decimal ToSeconds(TimeSpan duration) => duration.Ticks / TicksPerSecond;
}
