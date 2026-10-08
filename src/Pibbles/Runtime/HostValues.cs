using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// The one mapping between the runtime's values and what a host sees: <c>bool</c> is <see cref="bool"/>, <c>number</c> is
/// <see cref="decimal"/>, <c>string</c> is <see cref="string"/>, <c>duration</c> is <see cref="TimeSpan"/>, and an enum
/// member, an actor or a node is a <see cref="string"/> (the member's name, the actor's ID, the node's current name).
/// </summary>
internal static class HostValues
{
    /// <summary>Gives a value to the host. A duration beyond <see cref="TimeSpan"/>'s range clamps, with an overflow warning at <paramref name="location"/>.</summary>
    public static object ToHost(Value value, SourceLocation location, Action<RuntimeWarning> warn)
    {
        if (value.Type == TypeSymbol.Bool)
            return value.AsBool;

        if (value.Type == TypeSymbol.Number)
            return value.AsDecimal;

        if (value.Type == TypeSymbol.String || value.Type == TypeSymbol.Node)
            return value.AsString;

        return value.Type == TypeSymbol.Duration ? Durations.ToTimeSpan(value.AsDecimal, location, warn) : value.AsSymbol.Name;
    }

    /// <summary>Takes a value from the host, checking it against the type the story expects.</summary>
    /// <param name="result">What the host gave.</param>
    /// <param name="type">The type the story expects.</param>
    /// <param name="story">The story, for the members, actors and nodes a string can name.</param>
    /// <param name="subject">Who gave it, for the message: <c>`has_item` returned</c>.</param>
    /// <exception cref="InvalidOperationException">The value isn't of the type, or a string names nothing of it.</exception>
    public static Value FromHost(object? result, TypeSymbol type, Story story, string subject)
    {
        if (type == TypeSymbol.Bool)
            return result is bool flag ? Value.Bool(flag) : throw Wrong(subject, result, type);

        if (type == TypeSymbol.Number)
            return result is decimal number ? Value.Number(number) : throw Wrong(subject, result, type);

        if (type == TypeSymbol.Duration)
            return result is TimeSpan duration ? Value.Duration(Durations.ToSeconds(duration)) : throw Wrong(subject, result, type);

        string text = result as string ?? throw Wrong(subject, result, type);
        return FromText(text, type, story) ?? throw new InvalidOperationException($"{subject} \"{text}\", which isn't {Expected(type)}.");
    }

    /// <summary>
    /// A value of a type held as a string: a <c>string</c>, or the enum member, actor or node (current or old name) the text
    /// names. <see langword="null"/> if it names nothing of the type.
    /// </summary>
    public static Value? FromText(string text, TypeSymbol type, Story story)
    {
        if (type == TypeSymbol.String)
            return Value.String(text);

        if (type is EnumSymbol @enum)
            return @enum.Members.FirstOrDefault(member => member.Name == text) is { } member ? Value.Member(member, @enum) : null;

        if (type == TypeSymbol.Actor)
            return story.ActorSymbols.TryGetValue(text, out ActorSymbol? actor) ? Value.Actor(actor) : null;

        return story.CompiledNodes.ContainsKey(text) ? Value.Node(text)
            : story.Aliases.TryGetValue(text, out string? node) ? Value.Node(node)
            : null;
    }

    private static string Expected(TypeSymbol type) =>
        type is EnumSymbol @enum ? $"a member of `{@enum.Name}`"
        : type == TypeSymbol.Actor ? "a declared actor"
        : "a node or an old name of one";

    private static InvalidOperationException Wrong(string subject, object? result, TypeSymbol type) =>
        new($"{subject} {(result is null ? "null" : $"a {result.GetType().Name}")}, but the story expects {type.Describe()}.");
}
