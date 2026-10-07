using System.Globalization;
using System.Text;
using Pibbles.Compiler;

namespace Pibbles.Cli.Transcripts;

/// <summary>
/// Reads and writes values the way a script writes them: numbers normalized (invariant culture, no digit grouping, no
/// trailing zeros), strings quoted, durations with <c>s</c>, and enum members, actors and nodes bare. Values are the
/// host values of the public API (<see cref="StoryType.HostType"/>).
/// </summary>
internal static class ValueText
{
    private const string NumberFormat = "0.############################";

    private const decimal TicksPerSecond = 10_000_000m;

    /// <summary>A number as a script writes it.</summary>
    public static string FormatNumber(decimal number) => number.ToString(NumberFormat, CultureInfo.InvariantCulture);

    /// <summary>A duration in seconds, as a script writes it.</summary>
    public static string FormatSeconds(TimeSpan duration) => FormatNumber(duration.Ticks / TicksPerSecond) + "s";

    /// <summary>Writes a host value of a story type as a script would.</summary>
    public static string Show(object value, StoryType type) => type.Kind switch
    {
        StoryTypeKind.Bool => (bool)value ? "true" : "false",
        StoryTypeKind.Number => FormatNumber((decimal)value),
        StoryTypeKind.Duration => FormatSeconds((TimeSpan)value),
        StoryTypeKind.Text => Quote((string)value),
        _ => (string)value,
    };

    /// <summary>Escapes what would break a printed step across lines, or be read as an escape: backslashes and line breaks.</summary>
    public static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            escaped.Append(character switch
            {
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                _ when IsLineBreak(character) => $"\\u{(int)character:x4}",
                _ => character.ToString(),
            });
        }

        return escaped.ToString();
    }

    /// <summary>Reads a value of a known type, as a host value.</summary>
    /// <exception cref="TranscriptException">The text isn't a value of that type.</exception>
    public static object Parse(string text, StoryType type, Story story, string line)
    {
        TranscriptException Bad(string expected) => new($"I can't read `{line}`: `{text}` isn't {expected}.");

        switch (type.Kind)
        {
            case StoryTypeKind.Bool:
                return text is "true" or "false" ? text is "true" : throw Bad("`true` or `false`");

            case StoryTypeKind.Number:
                return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number) ? number : throw Bad("a number");

            case StoryTypeKind.Duration:
                return TryParseDuration(text, out TimeSpan duration) ? duration : throw Bad("a duration like `0.5s`");

            case StoryTypeKind.Text:
                return text.Length >= 2 && text[0] is '"' && text[^1] is '"' ? Unquote(text) : throw Bad("quoted text");

            case StoryTypeKind.Enum:
                return story.Enums.FirstOrDefault(@enum => @enum.Name == type.EnumName)?.Members.Contains(text) is true ? text : throw Bad($"a member of `{type.EnumName}`");

            case StoryTypeKind.Actor:
                return story.Actors.Any(actor => actor.Id == text) ? text : throw Bad("a declared actor");

            default:
                return story.Nodes.Any(node => node.Name == text || node.Aliases.Contains(text)) ? text : throw Bad("a node");
        }
    }

    /// <summary>A key that is the same for the same host values.</summary>
    public static string Key(object? value) => value switch
    {
        decimal number => "n" + FormatNumber(number),
        TimeSpan duration => "d" + duration.Ticks.ToString(CultureInfo.InvariantCulture),
        bool flag => flag ? "btrue" : "bfalse",
        _ => "s" + value,
    };

    private static bool IsLineBreak(char character) => character == (char)0x85 || character == (char)0x2028 || character == (char)0x2029;

    private static bool TryParseDuration(string text, out TimeSpan duration)
    {
        duration = default;
        const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        decimal seconds;
        if (text.EndsWith("ms", StringComparison.Ordinal))
        {
            if (!decimal.TryParse(text[..^2], Style, CultureInfo.InvariantCulture, out decimal milliseconds))
                return false;

            seconds = milliseconds / 1000m;
        }
        else if (!decimal.TryParse(text.EndsWith('s') ? text[..^1] : text, Style, CultureInfo.InvariantCulture, out seconds))
        {
            return false;
        }

        if (Math.Abs(seconds) > 900_000_000_000m)
            return false;

        duration = TimeSpan.FromTicks((long)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero));
        return true;
    }

    private static string Quote(string text) => "\"" + Escape(text).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string Unquote(string text)
    {
        var value = new StringBuilder();
        for (int i = 1; i < text.Length - 1; i++)
        {
            if (text[i] is '\\' && i + 1 < text.Length - 1)
            {
                i++;
                value.Append(text[i] switch { 'n' => '\n', 'r' => '\r', _ => text[i] });
            }
            else
            {
                value.Append(text[i]);
            }
        }

        return value.ToString();
    }
}
