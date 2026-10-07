using System.Collections.Immutable;

namespace Pibbles.Runtime;

/// <summary>A line of dialogue or narration, or the text of an option, ready to show.</summary>
/// <remarks>
/// <para>
/// Every position is a UTF-16 index into <see cref="Text"/>, the way .NET strings count, and a span's length counts the
/// same way. An adapter whose engine counts differently converts them. Positions are of the final text, after its
/// leading and trailing whitespace is trimmed; anything that fell in the trimmed whitespace moves to the nearest end.
/// </para>
/// <para>
/// Lines are compared by value: two lines are equal when all their fields are, including the contents of the arrays.
/// </para>
/// </remarks>
/// <param name="Id">The line's ID, or the option's. A line without an <c>#id</c> in its source has one made up by the compiler.</param>
/// <param name="Speaker">The speaker's actor ID, or <see langword="null"/> for narration and for options.</param>
/// <param name="SpeakerName">The speaker's display name, or <see langword="null"/> when there is no speaker.</param>
/// <param name="Text">The text, without markup, as plain characters: an icon is U+FFFC and a line break is <c>\n</c>.</param>
/// <param name="Spans">The markup spans over <paramref name="Text"/>, by start, an outer span before an inner one that starts with it.</param>
/// <param name="Markers">The points in <paramref name="Text"/> where the reveal does something, in the order the line has them.</param>
/// <param name="Icons">The icons in <paramref name="Text"/>, one for each U+FFFC.</param>
/// <param name="Tags">The line's tags, <c>#id</c> included.</param>
/// <param name="IsFallbackId">Whether <paramref name="Id"/> was made up by the compiler because the source has no <c>#id</c>. A fallback ID changes whenever the file is edited, so a host that records which lines the player has seen must skip it.</param>
public sealed record Line(
    string Id,
    string? Speaker,
    string? SpeakerName,
    string Text,
    ImmutableArray<Span> Spans,
    ImmutableArray<Marker> Markers,
    ImmutableArray<Icon> Icons,
    TagCollection Tags,
    bool IsFallbackId = false)
{
    /// <inheritdoc/>
    public bool Equals(Line? other) =>
        other is not null
        && Id == other.Id
        && Speaker == other.Speaker
        && SpeakerName == other.SpeakerName
        && Text == other.Text
        && Spans.SequenceEqual(other.Spans)
        && Markers.SequenceEqual(other.Markers)
        && Icons.SequenceEqual(other.Icons)
        && Tags.Equals(other.Tags)
        && IsFallbackId == other.IsFallbackId;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Speaker, Text, Spans.Length, Markers.Length, Icons.Length);
}

/// <summary>A markup span over a line's text: <c>[name args]…[/name]</c>.</summary>
/// <param name="Name">The markup's name.</param>
/// <param name="Start">Where the span starts.</param>
/// <param name="Length">How many characters it covers, which is 0 for an empty span.</param>
/// <param name="Arguments">The span's arguments, with defaults filled in.</param>
public sealed record Span(string Name, int Start, int Length, Arguments Arguments)
{
    /// <summary>Where the span ends, just after its last character.</summary>
    public int End => Start + Length;
}

/// <summary>
/// A point in a line's text where the reveal does something. Several markers can share a position, and keep the order
/// they have in the story. Match on the concrete records; later versions add kinds, so keep a default arm.
/// </summary>
public abstract record Marker
{
    private protected Marker(int position) => Position = position;

    /// <summary>Where in the text the marker is: the reveal reaches it just before showing the character at this index.</summary>
    public int Position { get; }
}

/// <summary><c>{w}</c>: wait for the player, then continue on the same page.</summary>
public sealed record InputWaitMarker(int Position) : Marker(Position);

/// <summary><c>{w 0.5}</c>: pause the reveal.</summary>
/// <param name="Position">Where the pause is.</param>
/// <param name="Duration">How long.</param>
public sealed record PauseMarker(int Position, TimeSpan Duration) : Marker(Position);

/// <summary><c>{p}</c>: wait for the player, clear the box, and continue.</summary>
public sealed record PageBreakMarker(int Position) : Marker(Position);

/// <summary><c>{speed x}</c> or <c>{speed}</c>: the reveal speed from here on.</summary>
/// <param name="Position">Where the speed changes.</param>
/// <param name="Factor">The speed in effect after the marker, relative to the player's setting: <c>x</c>, or 1 for <c>{speed}</c>. Speed starts at 1 on every line, with no marker.</param>
public sealed record SpeedMarker(int Position, decimal Factor) : Marker(Position);

/// <summary><c>{@command args}</c>: a command the reveal runs when it reaches this point.</summary>
/// <param name="Position">Where the command fires.</param>
/// <param name="Command">The command and its arguments.</param>
/// <param name="Waits">Whether the reveal waits for the command to finish.</param>
public sealed record CommandMarker(int Position, CommandInvocation Command, bool Waits) : Marker(Position);

/// <summary>An icon in a line's text, at the U+FFFC at <paramref name="Position"/>.</summary>
/// <param name="Position">The index of the U+FFFC.</param>
/// <param name="Name">The icon's name.</param>
public sealed record Icon(int Position, string Name);
