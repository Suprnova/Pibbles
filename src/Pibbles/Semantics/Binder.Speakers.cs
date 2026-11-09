using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Speakers: binding a text line's speaker and values, and reporting narration that almost looks like a speaker.</summary>
internal sealed partial class Binder
{
    private void BindTextLine(TextLineSyntax line)
    {
        if (line.Speaker is { Name: { } name } speaker)
        {
            if (symbols.Actors.TryGetValue(name.Text, out ActorSymbol? actor))
            {
                Refers(name, actor);
                BindValues(speaker.Values, actor.Channels, actor.DisplayName, line);
            }
            else
            {
                string meant = DiagnosticCatalog.DidYouMean(Suggestions.Closest(name.Text, symbols.Actors.Keys));
                Report(DiagnosticCatalog.UnknownSpeaker, name.Span, name.Text, meant, EscapeColon(PrefixOf(line)));
            }
        }
        else if (line.Speaker is { } narration)
        {
            BindValues(narration.Values, symbols.Narration.Channels, "Narration", line);
        }
        else
        {
            CheckNarration(line);
        }

        BindInline(line.Content);
        BindTags(line.Tags);
    }

    /// <summary>Binds each value in a speaker's parenthesis to the channel that has it, and reports a second value of one channel.</summary>
    private void BindValues(IReadOnlyList<NameSyntax> written, IReadOnlyList<SpeakerChannel> channels, string owner, TextLineSyntax line)
    {
        Dictionary<ChannelSymbol, NameSyntax>? set = null;
        foreach (NameSyntax value in written)
        {
            if (FindValue(channels, value.Text) is not var (channel, known))
            {
                ReportUnknownValue(channels, owner, value, line);
                continue;
            }

            Refers(value, known);
            set ??= [];
            if (set.TryGetValue(channel, out NameSyntax? first))
                Report(DiagnosticCatalog.TwoValuesInChannel, value.Span, first.Text, value.Text, owner, channel.Name);
            else
                set.Add(channel, value);
        }
    }

    /// <summary>Finds the value a speaker's parenthesis names, and the channel that has it.</summary>
    private static (ChannelSymbol Channel, ChannelValueSymbol Value)? FindValue(IReadOnlyList<SpeakerChannel> channels, string name)
    {
        foreach (SpeakerChannel has in channels)
        {
            foreach (ChannelValueSymbol known in has.Values)
            {
                if (known.Name == name)
                    return (has.Channel, known);
            }
        }

        return null;
    }

    private void ReportUnknownValue(IReadOnlyList<SpeakerChannel> channels, string owner, NameSyntax value, TextLineSyntax line)
    {
        string declaration = line.Speaker is { Name: { } actor } ? $"@actor {actor.Text}" : "@narration";
        string[] values = [.. channels.SelectMany(channel => channel.Values).Select(known => known.Name)];
        ChannelSymbol? dropped = symbols.Channels.Values.FirstOrDefault(channel => channels.All(has => has.Channel != channel) && channel.Defaults.Any(known => known.Name == value.Text));
        string help = dropped is not null ? $"{owner} has no `{dropped.Name}`, which has `{value.Text}`."
            : Suggestions.Closest(value.Text, values) is { } suggestion ? DiagnosticCatalog.DidYouMean(suggestion).TrimEnd()
            : values.Length switch
            {
                0 when symbols.Channels.Keys.FirstOrDefault() is { } channel => $"{owner} has no values to set. List {owner}'s values under `{declaration}`: `{channel}: neutral, happy`.",
                0 => $"{owner} has no values to set. Declare a channel such as `@channel pose`, then list {owner}'s values under `{declaration}`: `pose: neutral, happy`.",
                1 => $"{owner}'s only value is {Phrase.And(values)}.",
                _ => $"{owner}'s values are {Phrase.And(values)}.",
            };
        if (line.Speaker is { Name: null })
            help += $" If this is text, put a backslash before the `(`: `\\{PrefixOf(line)}`.";

        Report(DiagnosticCatalog.UnknownChannelValue, value.Span, owner, value.Text, help);
    }

    /// <summary>
    /// Reports narration that almost looks like a speaker, so a mistake never shows as narration: a parenthesis that
    /// isn't a list of names, anything between a declared actor's values and the colon, a word between a declared actor
    /// and the colon, or a declared actor with no space after the colon.
    /// </summary>
    private void CheckNarration(TextLineSyntax line)
    {
        if (SpeakerScanner.Scan(Tree.Source.Text, line.Span.Start, line.Span.End) is not { } prefix)
            return;

        string written = PrefixOf(line, prefix);
        string escaped = EscapeColon(written);
        if (prefix is { Parentheses: { } parentheses, Values: [] })
        {
            Report(DiagnosticCatalog.NotValues, parentheses, TextOf(parentheses), escaped);
        }
        else if (prefix is { Extra: { } extra, Parentheses: { } valuesParentheses } && IsActor(prefix))
        {
            Report(DiagnosticCatalog.AfterValues, extra, TextOf(extra), TextOf(valuesParentheses), escaped);
        }
        else if (prefix is { Extra: { } word, Name: { } actor, Parentheses: null } && IsActor(prefix))
        {
            Report(DiagnosticCatalog.WordBeforeColon, word, TextOf(word), TextOf(actor), escaped);
        }
        else if (IsActor(prefix))
        {
            int textEnd = line.Tags.Count > 0 ? line.Tags[0].Span.Start : line.Span.End;
            string after = TextOf(new(prefix.Colon + 1, textEnd - prefix.Colon - 1)).TrimEnd();
            Report(DiagnosticCatalog.NoSpaceAfterSpeaker, new(line.Span.Start, prefix.Colon + 1 - line.Span.Start), written[..^1], $"{escaped}{after}");
        }
    }

    private bool IsActor(SpeakerPrefix prefix) => prefix.Name is { } name && symbols.Actors.ContainsKey(TextOf(name));

    /// <summary>A text line's start through its speaker's colon, as written.</summary>
    private string PrefixOf(TextLineSyntax line) =>
        SpeakerScanner.Scan(Tree.Source.Text, line.Span.Start, line.Span.End) is { } prefix ? PrefixOf(line, prefix) : "";

    private string PrefixOf(TextLineSyntax line, SpeakerPrefix prefix) => TextOf(new(line.Span.Start, prefix.Colon + 1 - line.Span.Start));

    /// <summary><paramref name="prefix"/>, which ends in the colon, with a backslash before the colon.</summary>
    private static string EscapeColon(string prefix) => $"{prefix[..^1]}\\{prefix[^1]}";
}
