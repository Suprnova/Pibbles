namespace Pibbles.Semantics;

/// <summary>Every symbol in a story, with the prelude's: one table per kind of name, since each kind has its own namespace.</summary>
internal sealed class SymbolTable
{
    public Dictionary<string, ActorSymbol> Actors { get; } = [];

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

    /// <summary>Finds a built-in type or a declared enum by name.</summary>
    public TypeSymbol? FindType(string name) => TypeSymbol.BuiltIn.FirstOrDefault(type => type.Name == name) ?? Enums.GetValueOrDefault(name);
}
