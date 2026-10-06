using System.Globalization;

namespace Pibbles.Diagnostics;

/// <summary>
/// An argument that names a speaker in a message's prose. Until the speaker's display name is known, such as in a file
/// that's only been parsed, it reads with the speaker's ID.
/// </summary>
/// <param name="Speaker">The speaker's name: the ID as written, or the display name once it's known.</param>
/// <param name="Format">How the name reads in the message, with <c>{0}</c> for the name, such as <c>what {0} says</c>.</param>
internal sealed record SpeakerMention(string Speaker, string Format = "{0}")
{
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, Format, Speaker);
}
