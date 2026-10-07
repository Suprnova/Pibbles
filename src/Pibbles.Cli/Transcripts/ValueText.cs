using System.Globalization;
using System.Text;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Semantics;

namespace Pibbles.Cli.Transcripts;

/// <summary>Reads and writes values the way a script writes them: numbers normalized, strings quoted, durations with <c>s</c>, names bare.</summary>
internal static class ValueText
{
    /// <summary>Writes a value as a script would.</summary>
    public static string Show(Value value) =>
        value.Type == TypeSymbol.Bool ? (value.AsBool ? "true" : "false")
        : value.Type == TypeSymbol.Number ? LineRenderer.FormatNumber(value.AsDecimal)
        : value.Type == TypeSymbol.Duration ? LineRenderer.FormatNumber(value.AsDecimal) + "s"
        : value.Type == TypeSymbol.String ? Quote(value.AsString)
        : value.Type == TypeSymbol.Node ? value.AsString
        : value.AsSymbol.Name;

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

    private static bool IsLineBreak(char character) => character == (char)0x85 || character == (char)0x2028 || character == (char)0x2029;

    /// <summary>Reads a value of a known type.</summary>
    /// <exception cref="TranscriptException">The text isn't a value of that type.</exception>
    public static Value Parse(string text, TypeSymbol type, Story story, string line)
    {
        TranscriptException Bad(string expected) => new($"I can't read `{line}`: `{text}` isn't {expected}.");

        if (type == TypeSymbol.Bool)
            return text is "true" or "false" ? Value.Bool(text is "true") : throw Bad("`true` or `false`");

        if (type == TypeSymbol.Number)
            return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number) ? Value.Number(number) : throw Bad("a number");

        if (type == TypeSymbol.Duration)
            return TryParseDuration(text, out decimal seconds) ? Value.Duration(seconds) : throw Bad("a duration like `0.5s`");

        if (type == TypeSymbol.String)
            return text.Length >= 2 && text[0] is '"' && text[^1] is '"' ? Value.String(Unquote(text)) : throw Bad("quoted text");

        if (type is EnumSymbol @enum)
            return @enum.Members.FirstOrDefault(member => member.Name == text) is { } found ? Value.Member(found, @enum) : throw Bad($"a member of `{@enum.Name}`");

        if (type == TypeSymbol.Actor)
            return story.Actors.TryGetValue(text, out ActorSymbol? actor) ? Value.Actor(actor) : throw Bad("a declared actor");

        string node = story.CompiledNodes.ContainsKey(text) ? text : story.Aliases.GetValueOrDefault(text) ?? throw Bad("a node");
        return Value.Node(node);
    }

    /// <summary>The value as the object a host function receives or returns, with the mapping <see cref="HostFunctions"/> uses.</summary>
    public static object ToHost(Value value) =>
        value.Type == TypeSymbol.Bool ? value.AsBool
        : value.Type == TypeSymbol.Number ? value.AsDecimal
        : value.Type == TypeSymbol.Duration ? Durations.ToTimeSpan(value.AsDecimal, default, _ => { })
        : value.Type == TypeSymbol.String || value.Type == TypeSymbol.Node ? value.AsString
        : value.AsSymbol.Name;

    /// <summary>A key that is the same for the same host values.</summary>
    public static string Key(object? value) => value switch
    {
        decimal number => "n" + LineRenderer.FormatNumber(number),
        TimeSpan duration => "d" + duration.Ticks.ToString(CultureInfo.InvariantCulture),
        bool flag => flag ? "btrue" : "bfalse",
        _ => "s" + value,
    };

    private static bool TryParseDuration(string text, out decimal seconds)
    {
        seconds = 0;
        const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (text.EndsWith("ms", StringComparison.Ordinal))
        {
            bool parsed = decimal.TryParse(text[..^2], Style, CultureInfo.InvariantCulture, out decimal milliseconds);
            seconds = milliseconds / 1000m;
            return parsed;
        }

        return decimal.TryParse(text.EndsWith('s') ? text[..^1] : text, Style, CultureInfo.InvariantCulture, out seconds);
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
