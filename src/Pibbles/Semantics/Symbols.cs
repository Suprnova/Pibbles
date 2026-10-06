using Pibbles.Diagnostics;

namespace Pibbles.Semantics;

/// <summary>Something a story declares, or Pibbles has built in, that a name can refer to.</summary>
/// <param name="name">The symbol's name as declared: without the <c>$</c> of a variable, and in full for a node.</param>
/// <param name="location">Where the name is declared, or <see langword="null"/> for a built-in symbol.</param>
internal abstract class Symbol(string name, SourceLocation? location)
{
    public string Name { get; } = name;

    public SourceLocation? Location { get; } = location;
}

/// <summary>A type: one of the built-in types, a declared enum, or the error type.</summary>
internal abstract class TypeSymbol(string name, SourceLocation? location) : Symbol(name, location)
{
    public static TypeSymbol Bool { get; } = new BuiltInType("bool");

    public static TypeSymbol Number { get; } = new BuiltInType("number");

    public static TypeSymbol String { get; } = new BuiltInType("string");

    public static TypeSymbol Duration { get; } = new BuiltInType("duration");

    public static TypeSymbol Node { get; } = new BuiltInType("node");

    public static TypeSymbol Actor { get; } = new BuiltInType("actor");

    /// <summary>
    /// The type of anything whose type can't be known, because of a problem that has already been reported. It converts
    /// to and from every type, so the problem is reported once.
    /// </summary>
    public static TypeSymbol Error { get; } = new BuiltInType("?");

    public static IReadOnlyList<TypeSymbol> BuiltIn { get; } = [Bool, Number, String, Duration, Node, Actor];

    private sealed class BuiltInType(string name) : TypeSymbol(name, null);
}

/// <summary><c>@enum name: a, b, c</c>.</summary>
internal sealed class EnumSymbol(string name, SourceLocation? location, IReadOnlyList<EnumMemberSymbol> members) : TypeSymbol(name, location)
{
    public IReadOnlyList<EnumMemberSymbol> Members { get; } = members;
}

internal sealed class EnumMemberSymbol(string name, SourceLocation? location) : Symbol(name, location);

/// <summary><c>@actor id</c> and its properties.</summary>
internal sealed class ActorSymbol(string name, SourceLocation? location, string displayName, IReadOnlyList<PoseSymbol> poses) : Symbol(name, location)
{
    /// <summary>The name the player sees: the <c>name:</c> property, or the ID.</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>The actor's poses. The first is the default.</summary>
    public IReadOnlyList<PoseSymbol> Poses { get; } = poses;
}

internal sealed class PoseSymbol(string name, SourceLocation? location) : Symbol(name, location);

internal sealed class VariableSymbol(string name, SourceLocation? location, TypeSymbol type) : Symbol(name, location)
{
    public TypeSymbol Type { get; } = type;
}

/// <summary>A parameter of a command, markup or function.</summary>
internal sealed class ParameterSymbol(string name, SourceLocation? location, TypeSymbol type, bool isOptional) : Symbol(name, location)
{
    public TypeSymbol Type { get; } = type;

    /// <summary>Whether the parameter has a default, so an argument for it can be left out.</summary>
    public bool IsOptional { get; } = isOptional;
}

internal sealed class CommandSymbol(string name, SourceLocation? location, IReadOnlyList<ParameterSymbol> parameters, bool isInline, bool waits) : Symbol(name, location)
{
    public IReadOnlyList<ParameterSymbol> Parameters { get; } = parameters;

    /// <summary>Whether the command can appear inside text as <c>{@name …}</c>.</summary>
    public bool IsInline { get; } = isInline;

    /// <summary>Whether the story waits for the command to finish unless a line says <c>nowait</c>.</summary>
    public bool Waits { get; } = waits;
}

internal sealed class MarkupSymbol(string name, SourceLocation? location, IReadOnlyList<ParameterSymbol> parameters) : Symbol(name, location)
{
    public IReadOnlyList<ParameterSymbol> Parameters { get; } = parameters;
}

internal sealed class IconSymbol(string name, SourceLocation? location) : Symbol(name, location);

internal sealed class TagSymbol(string name, SourceLocation? location, TypeSymbol? valueType, bool allowsEmpty) : Symbol(name, location)
{
    /// <summary>The type of the tag's value, or <see langword="null"/> for a flag, which takes no value.</summary>
    public TypeSymbol? ValueType { get; } = valueType;

    /// <summary>Whether the value may be empty (<c>#box:</c>), which the host receives as <see langword="null"/>.</summary>
    public bool AllowsEmpty { get; } = allowsEmpty;
}

internal sealed class FunctionSymbol(string name, SourceLocation? location, IReadOnlyList<ParameterSymbol> parameters, TypeSymbol returnType) : Symbol(name, location)
{
    public IReadOnlyList<ParameterSymbol> Parameters { get; } = parameters;

    public TypeSymbol ReturnType { get; } = returnType;
}

/// <summary>A node, by its full name, with its former names from <c>#was:</c>.</summary>
internal sealed class NodeSymbol(string name, SourceLocation? location, IReadOnlyList<string> aliases) : Symbol(name, location)
{
    /// <summary>The node's former names, in full.</summary>
    public IReadOnlyList<string> Aliases { get; } = aliases;
}
