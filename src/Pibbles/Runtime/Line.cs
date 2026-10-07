using System.Collections.Immutable;

namespace Pibbles.Runtime;

/// <summary>A line of dialogue or narration, or the text of an option, ready to show.</summary>
/// <param name="Id">The line's ID, or the option's. A line without an <c>#id</c> in its source has one made up by the compiler.</param>
/// <param name="Speaker">The speaker's actor ID, or <see langword="null"/> for narration and for options.</param>
/// <param name="SpeakerName">The speaker's display name, or <see langword="null"/> when there is no speaker.</param>
/// <param name="Text">The text, without markup, as plain characters: an icon is U+FFFC and a line break is <c>\n</c>.</param>
/// <param name="Spans">The markup spans over <paramref name="Text"/>. Empty for now.</param>
/// <param name="Markers">The points in <paramref name="Text"/> where the reveal does something. Empty for now.</param>
/// <param name="Icons">The icons in <paramref name="Text"/>. Empty for now.</param>
/// <param name="Tags">The line's tags. Empty for now.</param>
public sealed record Line(
    string Id,
    string? Speaker,
    string? SpeakerName,
    string Text,
    ImmutableArray<Span> Spans,
    ImmutableArray<Marker> Markers,
    ImmutableArray<Icon> Icons,
    ImmutableArray<Tag> Tags);

/// <summary>A markup span over a line's text. Not filled in yet.</summary>
public sealed record Span;

/// <summary>A point in a line's text where the reveal does something. Not filled in yet.</summary>
public sealed record Marker;

/// <summary>An icon in a line's text. Not filled in yet.</summary>
public sealed record Icon;

/// <summary>A tag on a line. Not filled in yet.</summary>
public sealed record Tag;
