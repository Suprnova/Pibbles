using Pibbles.Diagnostics;

namespace Pibbles.Semantics;

/// <summary>Something a story declares, or Pibbles has built in, that a name can refer to.</summary>
public abstract class Symbol
{
    private protected Symbol(string name, SourceLocation? location)
    {
        Name = name;
        Location = location;
    }

    /// <summary>The symbol's name as declared: without the <c>$</c> of a variable, and in full for a node.</summary>
    public string Name { get; }

    /// <summary>Where the symbol's name is declared, or <see langword="null"/> for a symbol built into Pibbles.</summary>
    public SourceLocation? Location { get; }
}

/// <summary>A type: one of the built-in types (<c>bool</c>, <c>number</c>, <c>string</c>, <c>duration</c>, <c>node</c> and <c>actor</c>), or an enum the story declares.</summary>
public abstract class TypeSymbol : Symbol
{
    private protected TypeSymbol(string name, SourceLocation? location)
        : base(name, location)
    {
    }

    /// <summary><c>true</c> or <c>false</c>.</summary>
    internal static TypeSymbol Bool { get; } = new BuiltInType("bool");

    /// <summary>A 64-bit floating-point number.</summary>
    internal static TypeSymbol Number { get; } = new BuiltInType("number");

    /// <summary>Text.</summary>
    internal static TypeSymbol String { get; } = new BuiltInType("string");

    /// <summary>A length of time, such as <c>0.5s</c>.</summary>
    internal static TypeSymbol Duration { get; } = new BuiltInType("duration");

    /// <summary>A node, by name.</summary>
    internal static TypeSymbol Node { get; } = new BuiltInType("node");

    /// <summary>An actor, by ID.</summary>
    internal static TypeSymbol Actor { get; } = new BuiltInType("actor");

    /// <summary>
    /// The type of anything whose type can't be known, because of a problem that has already been reported. It converts
    /// to and from every type, so the problem is reported once.
    /// </summary>
    internal static TypeSymbol Error { get; } = new BuiltInType("?");

    internal static IReadOnlyList<TypeSymbol> BuiltIn { get; } = [Bool, Number, String, Duration, Node, Actor];

    /// <summary>The type as messages write it, with its article: <c>a `number`</c>, <c>an `actor`</c>.</summary>
    internal string Describe() => $"{(Name[0] is 'a' or 'e' or 'i' or 'o' or 'u' ? "an" : "a")} `{Name}`";

    private sealed class BuiltInType(string name) : TypeSymbol(name, null);
}

/// <summary>An enum: <c>@enum name: a, b, c</c>.</summary>
public sealed class EnumSymbol : TypeSymbol
{
    internal EnumSymbol(string name, SourceLocation? location, IReadOnlyList<EnumMemberSymbol> members)
        : base(name, location) => Members = members;

    /// <summary>The enum's members, in order.</summary>
    public IReadOnlyList<EnumMemberSymbol> Members { get; }
}

/// <summary>One member of an enum.</summary>
public sealed class EnumMemberSymbol : Symbol
{
    internal EnumMemberSymbol(string name, SourceLocation? location)
        : base(name, location)
    {
    }
}

/// <summary>An actor: <c>@actor id</c> and its properties.</summary>
public sealed class ActorSymbol : Symbol
{
    internal ActorSymbol(string name, SourceLocation? location, string displayName, IReadOnlyList<PoseSymbol> poses)
        : base(name, location)
    {
        DisplayName = displayName;
        Poses = poses;
    }

    /// <summary>The name the player sees: the <c>name:</c> property, or the ID.</summary>
    public string DisplayName { get; }

    /// <summary>The actor's poses. The first is the default.</summary>
    public IReadOnlyList<PoseSymbol> Poses { get; }
}

/// <summary>One of an actor's poses.</summary>
public sealed class PoseSymbol : Symbol
{
    internal PoseSymbol(string name, SourceLocation? location)
        : base(name, location)
    {
    }
}

/// <summary>A variable: <c>@var $name = value</c>.</summary>
public sealed class VariableSymbol : Symbol
{
    internal VariableSymbol(string name, SourceLocation? location, TypeSymbol type)
        : base(name, location) => Type = type;

    /// <summary>The variable's type, written out or from its starting value.</summary>
    public TypeSymbol Type { get; }
}

/// <summary>A parameter of a command, markup or function.</summary>
public sealed class ParameterSymbol : Symbol
{
    internal ParameterSymbol(string name, SourceLocation? location, TypeSymbol type, bool isOptional)
        : base(name, location)
    {
        Type = type;
        IsOptional = isOptional;
    }

    /// <summary>The parameter's type.</summary>
    public TypeSymbol Type { get; }

    /// <summary>Whether the parameter has a default, so an argument for it can be left out.</summary>
    public bool IsOptional { get; }
}

/// <summary>A command the host carries out: <c>@command name(params)</c>.</summary>
public sealed class CommandSymbol : Symbol
{
    internal CommandSymbol(string name, SourceLocation? location, IReadOnlyList<ParameterSymbol> parameters, bool isInline, bool waits)
        : base(name, location)
    {
        Parameters = parameters;
        IsInline = isInline;
        Waits = waits;
    }

    /// <summary>The command's parameters, in order.</summary>
    public IReadOnlyList<ParameterSymbol> Parameters { get; }

    /// <summary>Whether the command can appear inside text as <c>{@name …}</c>.</summary>
    public bool IsInline { get; }

    /// <summary>Whether the story waits for the command to finish unless a line says <c>nowait</c>.</summary>
    public bool Waits { get; }
}

/// <summary>Markup for a span of text: <c>@markup name(params)</c>, or one built into Pibbles.</summary>
public sealed class MarkupSymbol : Symbol
{
    internal MarkupSymbol(string name, SourceLocation? location, IReadOnlyList<ParameterSymbol> parameters)
        : base(name, location) => Parameters = parameters;

    /// <summary>The markup's parameters, in order.</summary>
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
}

/// <summary>An icon text can show with <c>{icon name}</c>.</summary>
public sealed class IconSymbol : Symbol
{
    internal IconSymbol(string name, SourceLocation? location)
        : base(name, location)
    {
    }
}

/// <summary>A tag the host reads: <c>@tag name</c> or <c>@tag name: type</c>.</summary>
public sealed class TagSymbol : Symbol
{
    internal TagSymbol(string name, SourceLocation? location, TypeSymbol? valueType, bool allowsEmpty)
        : base(name, location)
    {
        ValueType = valueType;
        AllowsEmpty = allowsEmpty;
    }

    /// <summary>The type of the tag's value, or <see langword="null"/> for a flag, which takes no value.</summary>
    public TypeSymbol? ValueType { get; }

    /// <summary>Whether the value may be empty (<c>#box:</c>), which the host receives as <see langword="null"/>.</summary>
    public bool AllowsEmpty { get; }
}

/// <summary>A function the host provides, <c>@function name(params) -> type</c>, or one built into Pibbles.</summary>
public sealed class FunctionSymbol : Symbol
{
    internal FunctionSymbol(string name, SourceLocation? location, IReadOnlyList<ParameterSymbol> parameters, TypeSymbol returnType)
        : base(name, location)
    {
        Parameters = parameters;
        ReturnType = returnType;
    }

    /// <summary>The function's parameters, in order.</summary>
    public IReadOnlyList<ParameterSymbol> Parameters { get; }

    /// <summary>The type the function returns.</summary>
    public TypeSymbol ReturnType { get; }
}

/// <summary>A node, by its full name, with its former names from <c>#was:</c>.</summary>
public sealed class NodeSymbol : Symbol
{
    internal NodeSymbol(string name, SourceLocation? location, IReadOnlyList<string> aliases)
        : base(name, location) => Aliases = aliases;

    /// <summary>The node's former names, in full.</summary>
    public IReadOnlyList<string> Aliases { get; }
}
