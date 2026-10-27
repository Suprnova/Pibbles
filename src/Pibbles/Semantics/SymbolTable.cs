namespace Pibbles.Semantics;

/// <summary>Every symbol in a story, with the prelude's: one table per kind of name, since each kind has its own namespace.</summary>
internal sealed class SymbolTable
{
    public Dictionary<string, ActorSymbol> Actors { get; } = [];

    public Dictionary<string, ChannelSymbol> Channels { get; } = [];

    /// <summary>What narration has. It's the story's <c>@narration</c> block, or the default once every declaration is made.</summary>
    public NarrationSymbol Narration { get; set; } = null!;

    public Dictionary<string, EnumSymbol> Enums { get; } = [];

    public Dictionary<string, VariableSymbol> Variables { get; } = [];

    public Dictionary<string, CommandSymbol> Commands { get; } = [];

    public Dictionary<string, MarkupSymbol> Markup { get; } = [];

    public Dictionary<string, IconSymbol> Icons { get; } = [];

    public Dictionary<string, TagSymbol> Tags { get; } = [];

    public Dictionary<string, FunctionSymbol> Functions { get; } = [];

    /// <summary>Nodes by their current full name.</summary>
    public Dictionary<string, NodeSymbol> Nodes { get; } = [];

    /// <summary>Nodes by each of their former full names.</summary>
    public Dictionary<string, NodeSymbol> Aliases { get; } = [];

    /// <summary>Every symbol, with the channel values, enum members and parameters that belong to them.</summary>
    public IEnumerable<Symbol> All() =>
    [
        .. Actors.Values.SelectMany(actor => (IEnumerable<Symbol>)[actor, .. actor.Channels.SelectMany(channel => channel.Values.Where(value => !channel.Channel.Defaults.Contains(value)))]),
        .. Channels.Values.SelectMany(channel => (IEnumerable<Symbol>)[channel, .. channel.Defaults]),
        .. Narration.Location is null ? [] : (IEnumerable<Symbol>)[Narration, .. Narration.Channels.SelectMany(channel => channel.Values.Where(value => !channel.Channel.Defaults.Contains(value)))],
        .. Enums.Values.SelectMany(@enum => (IEnumerable<Symbol>)[@enum, .. @enum.Members]),
        .. Variables.Values,
        .. Commands.Values.SelectMany(command => (IEnumerable<Symbol>)[command, .. command.Parameters]),
        .. Markup.Values.SelectMany(markup => (IEnumerable<Symbol>)[markup, .. markup.Parameters]),
        .. Icons.Values,
        .. Tags.Values,
        .. Functions.Values.SelectMany(function => (IEnumerable<Symbol>)[function, .. function.Parameters]),
        .. Nodes.Values,
    ];

    /// <summary>Finds a built-in type or a declared enum by name.</summary>
    public TypeSymbol? FindType(string name) => TypeSymbol.BuiltIn.FirstOrDefault(type => type.Name == name) ?? Enums.GetValueOrDefault(name);
}
