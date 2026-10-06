namespace Pibbles.Semantics;

/// <summary>The kinds of name a story declares. Each kind has its own namespace and its own reserved words.</summary>
internal enum SymbolKind
{
    Actor,
    Pose,
    Enum,
    EnumMember,
    Variable,
    Command,
    Markup,
    Icon,
    Tag,
    Function,
    Parameter,
    Node,
}

internal static class SymbolKindExtensions
{
    /// <summary>The kind as messages write it, with its article: <c>an actor</c>, <c>markup</c>.</summary>
    public static string Describe(this SymbolKind kind) => kind switch
    {
        SymbolKind.Actor => "an actor",
        SymbolKind.Pose => "a pose",
        SymbolKind.Enum => "an enum",
        SymbolKind.EnumMember => "an enum member",
        SymbolKind.Variable => "a variable",
        SymbolKind.Command => "a command",
        SymbolKind.Markup => "markup",
        SymbolKind.Icon => "an icon",
        SymbolKind.Tag => "a tag",
        SymbolKind.Function => "a function",
        SymbolKind.Parameter => "a parameter",
        _ => "a node",
    };
}
