using Pibbles.Semantics;

namespace Pibbles.Compiler;

/// <summary>The kinds of type a story's values have.</summary>
public enum StoryTypeKind
{
    /// <summary><c>bool</c>: a <see cref="bool"/> for a host.</summary>
    Bool,

    /// <summary><c>number</c>: a <see cref="decimal"/> for a host.</summary>
    Number,

    /// <summary><c>string</c>: a <see cref="string"/> for a host.</summary>
    Text,

    /// <summary><c>duration</c>: a <see cref="TimeSpan"/> for a host.</summary>
    Duration,

    /// <summary>One of the story's enums: the member's name, as a <see cref="string"/>, for a host.</summary>
    Enum,

    /// <summary><c>actor</c>: the actor's ID, as a <see cref="string"/>, for a host.</summary>
    Actor,

    /// <summary><c>node</c>: the node's current name, as a <see cref="string"/>, for a host.</summary>
    Node,
}

/// <summary>A type of the story's, and how a host sees its values.</summary>
/// <remarks>
/// A host sees every value the same way, wherever it meets one: <c>bool</c> as <see cref="bool"/>, <c>number</c> as
/// <see cref="decimal"/>, <c>string</c> as <see cref="string"/>, <c>duration</c> as <see cref="TimeSpan"/>, and an enum
/// member, actor or node as a <see cref="string"/>. That is <see cref="HostType"/>.
/// </remarks>
/// <param name="Kind">Which kind of type it is.</param>
/// <param name="EnumName">The enum's name, for an enum; otherwise <see langword="null"/>.</param>
public sealed record StoryType(StoryTypeKind Kind, string? EnumName = null)
{
    /// <summary>The .NET type a host sees values of this type as.</summary>
    public Type HostType => Kind switch
    {
        StoryTypeKind.Bool => typeof(bool),
        StoryTypeKind.Number => typeof(decimal),
        StoryTypeKind.Duration => typeof(TimeSpan),
        _ => typeof(string),
    };

    /// <summary>The type as the story writes it: <c>number</c>, or an enum's name.</summary>
    public override string ToString() => EnumName ?? (Kind is StoryTypeKind.Text ? "string" : Kind.ToString().ToLowerInvariant());

    internal static StoryType Of(TypeSymbol type) =>
        type == TypeSymbol.Bool ? new(StoryTypeKind.Bool)
        : type == TypeSymbol.Number ? new(StoryTypeKind.Number)
        : type == TypeSymbol.Duration ? new(StoryTypeKind.Duration)
        : type == TypeSymbol.Actor ? new(StoryTypeKind.Actor)
        : type == TypeSymbol.Node ? new(StoryTypeKind.Node)
        : type is EnumSymbol ? new(StoryTypeKind.Enum, type.Name)
        : new(StoryTypeKind.Text);
}

/// <summary>A parameter of a function, command or markup.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="Type">Its type.</param>
public sealed record StoryParameter(string Name, StoryType Type);

/// <summary>A function the story declares, which the host provides.</summary>
/// <param name="Name">The function's name.</param>
/// <param name="Parameters">Its parameters, in order.</param>
/// <param name="ReturnType">The type it returns.</param>
public sealed record FunctionInfo(string Name, IReadOnlyList<StoryParameter> Parameters, StoryType ReturnType);

/// <summary>A variable the story declares.</summary>
/// <param name="Name">The variable's name, without the <c>$</c>.</param>
/// <param name="Type">Its type.</param>
public sealed record VariableInfo(string Name, StoryType Type);

/// <summary>An actor the story declares.</summary>
/// <param name="Id">The actor's ID.</param>
/// <param name="DisplayName">The name the player sees.</param>
/// <param name="Poses">The actor's poses, the first being the default.</param>
public sealed record ActorInfo(string Id, string DisplayName, IReadOnlyList<string> Poses);

/// <summary>An enum the story declares.</summary>
/// <param name="Name">The enum's name.</param>
/// <param name="Members">Its members, in order.</param>
public sealed record EnumInfo(string Name, IReadOnlyList<string> Members);
