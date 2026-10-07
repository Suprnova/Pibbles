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
        if (type == TypeSymbol.String)
            return Value.String(text);

        if (type is EnumSymbol @enum)
            return @enum.Members.FirstOrDefault(member => member.Name == text) is { } member ? Value.Member(member, @enum) : throw NotA(subject, text, $"a member of `{@enum.Name}`");

        if (type == TypeSymbol.Actor)
            return story.ActorSymbols.TryGetValue(text, out ActorSymbol? actor) ? Value.Actor(actor) : throw NotA(subject, text, "a declared actor");

        string node = story.CompiledNodes.ContainsKey(text) ? text : story.Aliases.GetValueOrDefault(text) ?? throw NotA(subject, text, "a node or an old name of one");
        return Value.Node(node);
    }

    private static InvalidOperationException Wrong(string subject, object? result, TypeSymbol type) =>
        new($"{subject} {(result is null ? "null" : $"a {result.GetType().Name}")}, but the story expects {type.Describe()}.");

    private static InvalidOperationException NotA(string subject, string text, string expected) =>
        new($"{subject} \"{text}\", which isn't {expected}.");
}
