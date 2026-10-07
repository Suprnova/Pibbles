using Pibbles.Diagnostics;

namespace Pibbles.Runtime;

/// <summary>What a <see cref="RuntimeWarning"/> is about. New kinds can be added, so a host that switches on it keeps a default arm.</summary>
public enum RuntimeWarningKind
{
    /// <summary>A division or remainder by zero, which gave 0.</summary>
    DivisionByZero,

    /// <summary>A number or duration too large to hold, which was clamped to the nearest limit.</summary>
    Overflow,

    /// <summary>A <c>@wait</c> that wasn't for more than zero time, which the runner skipped.</summary>
    NonPositiveWait,

    /// <summary>A dialogue that ran a very long time without showing anything, which the runner ended.</summary>
    InfiniteLoop,
}

/// <summary>
/// A problem in the story's content that the story carried on from, such as dividing by zero. It isn't a
/// <see cref="Diagnostic"/>, which only reports what analysis finds before the story runs, and it isn't an exception,
/// which is for the host's own mistakes.
/// </summary>
/// <param name="Kind">What the warning is about, for a host that filters.</param>
/// <param name="Message">What happened and what the story did instead, written for the story's writers.</param>
/// <param name="Location">Where in the story it happened.</param>
public sealed record RuntimeWarning(RuntimeWarningKind Kind, string Message, SourceLocation Location);
