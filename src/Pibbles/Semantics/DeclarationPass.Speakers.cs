using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Speakers: declaring channels, actors and narration, and working out the channels and values each has.</summary>
internal sealed partial class DeclarationPass
{
    private void DeclareChannels(FileSyntax file)
    {
        foreach (ChannelDeclarationSyntax declaration in file.Declarations.OfType<ChannelDeclarationSyntax>())
        {
            List<ChannelValueSymbol> defaults = DeclareNames(declaration.Values, SymbolKind.ChannelValue,
                $"the values of `{declaration.Name.Text}`", value => new ChannelValueSymbol(value.Text, Location(value.Span)));
            bool isDuplicate = symbols.Channels.ContainsKey(declaration.Name.Text);
            foreach (ChannelValueSymbol value in defaults)
            {
                if (!isDuplicate && symbols.Channels.Values.FirstOrDefault(other => other.Defaults.Any(known => known.Name == value.Name)) is { } sharing)
                    ReportSharedValue(declaration.Values.First(candidate => candidate.Text == value.Name), $"`{sharing.Name}`", declaration.Name.Text);
            }

            Declare(symbols.Channels, new ChannelSymbol(declaration.Name.Text, Location(declaration.Name.Span), defaults), declaration.Name, SymbolKind.Channel);
        }
    }

    private void ReportSharedValue(NameSyntax value, string holder, string channel) =>
        Report(DiagnosticCatalog.ValueInTwoChannels, value.Span, value.Text, holder, channel);

    private void DeclareActor(ActorDeclarationSyntax actor)
    {
        string displayName = actor.DisplayName ?? actor.Name.Text;
        string owner = actor.Name.IsMissing ? "this actor" : displayName;
        List<SpeakerChannel> channels = ResolveChannels(actor.Channels, owner);
        Declare(symbols.Actors, new ActorSymbol(actor.Name.Text, Location(actor.Name.Span), displayName, channels), actor.Name, SymbolKind.Actor);
    }

    private void DeclareNarration(NarrationDeclarationSyntax narration)
    {
        var keyword = new TextSpan(narration.Span.Start, "@narration".Length);
        if (!builtIn)
        {
            if (narrationLocation is { } first)
            {
                Report(DiagnosticCatalog.SecondNarration, keyword, Where(first));
                return;
            }

            narrationLocation = Tree.Source.GetLocation(keyword);
        }

        symbols.Narration = new NarrationSymbol(Location(keyword), narration.DisplayName, ResolveChannels(narration.Channels, "Narration"));
        DeclaresIfWritten(keyword, symbols.Narration);
    }

    /// <summary>
    /// Works out the channels an actor or narration has: every channel with its defaults, then the block applied. A channel
    /// the block drops is left out, one it replaces has the block's values, and one it adds to has the defaults and the
    /// block's values. A channel is mentioned once in a block, and the values of an actor's channels all differ.
    /// </summary>
    private List<SpeakerChannel> ResolveChannels(IReadOnlyList<ChannelEditSyntax> edits, string owner)
    {
        Dictionary<ChannelSymbol, List<ChannelValueSymbol>?> lists = symbols.Channels.Values.ToDictionary(channel => channel, channel => (List<ChannelValueSymbol>?)[.. channel.Defaults]);
        HashSet<string> written = [];
        List<(NameSyntax Syntax, ChannelValueSymbol Value, ChannelSymbol Channel)> introduced = [];

        foreach (ChannelEditSyntax edit in edits)
        {
            if (FindChannel(edit.Channel, written, owner) is not { } channel)
                continue;

            if (edit.Edit is ChannelEdit.Drop)
            {
                lists[channel] = null;
                continue;
            }

            List<ChannelValueSymbol> values = DeclareNames(edit.Values, SymbolKind.ChannelValue,
                $"{owner}'s `{channel.Name}` values", value => new ChannelValueSymbol(value.Text, Location(value.Span)));
            List<ChannelValueSymbol> kept = edit.Edit is ChannelEdit.Add ? lists[channel]! : [];
            foreach (ChannelValueSymbol value in values)
            {
                NameSyntax syntax = edit.Values.First(candidate => candidate.Text == value.Name);
                if (kept.Any(known => known.Name == value.Name))
                    Report(DiagnosticCatalog.RepeatedName, syntax.Span, value.Name, $"{owner}'s `{channel.Name}` values");
                else
                    kept.Add(value);

                introduced.Add((syntax, value, channel));
            }

            lists[channel] = kept;
        }

        List<SpeakerChannel> channels = [.. lists.Where(list => list.Value is not null).Select(list => new SpeakerChannel(list.Key, list.Value!))];
        for (int index = 0; index < introduced.Count; index++)
        {
            (NameSyntax syntax, ChannelValueSymbol value, ChannelSymbol channel) = introduced[index];
            bool IntroducedLater(SpeakerChannel other) =>
                introduced.Skip(index + 1).Any(later => later.Channel == other.Channel && later.Value.Name == value.Name);

            if (channels.FirstOrDefault(other => other.Channel != channel && other.Values.Any(known => known.Name == value.Name) && !IntroducedLater(other)) is { } sharing)
                ReportSharedValue(syntax, $"{owner}'s `{sharing.Channel.Name}`", channel.Name);
        }

        return channels;
    }

    /// <summary>Finds the channel a block line names, reporting it if it isn't declared or the block already mentioned it.</summary>
    private ChannelSymbol? FindChannel(NameSyntax name, HashSet<string> written, string owner)
    {
        if (name.IsMissing)
            return null;

        if (!symbols.Channels.TryGetValue(name.Text, out ChannelSymbol? channel))
        {
            ReportWithOptionalHelp(DiagnosticCatalog.UnknownChannel, name.Span, name.Text, Suggestions.Closest(name.Text, symbols.Channels.Keys));
            return null;
        }

        Refers(name.Span, channel);
        if (written.Add(name.Text))
            return channel;

        Report(DiagnosticCatalog.RepeatedName, name.Span, name.Text, $"the channels in {owner}'s block");
        return null;
    }
}
