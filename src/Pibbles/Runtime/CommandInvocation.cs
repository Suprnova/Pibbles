using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// A command to carry out, with its arguments, which are read by parameter name with an accessor for the parameter's
/// type. Every parameter has a value, since a default fills any argument the story left out.
/// </summary>
/// <remarks>
/// An accessor throws <see cref="ArgumentException"/> for a name the command has no parameter for, and
/// <see cref="InvalidOperationException"/> if it isn't the accessor for that parameter's type: both are the host's mistakes.
/// </remarks>
public sealed class CommandInvocation
{
    private readonly SourceLocation location;
    private readonly Action<RuntimeWarning> warn;

    internal CommandInvocation(CommandSymbol command, IReadOnlyList<Value> arguments, SourceLocation location, Action<RuntimeWarning> warn)
    {
        Symbol = command;
        Arguments = arguments;
        this.location = location;
        this.warn = warn;
    }

    /// <summary>The command's name, without the <c>@</c>.</summary>
    public string Name => Symbol.Name;

    internal CommandSymbol Symbol { get; }

    /// <summary>The arguments, in the order of the command's parameters.</summary>
    internal IReadOnlyList<Value> Arguments { get; }

    /// <summary>The value of a <c>bool</c> parameter.</summary>
    public bool GetBool(string parameter) => Get(parameter, TypeSymbol.Bool).AsBool;

    /// <summary>The value of a <c>string</c> parameter.</summary>
    public string GetString(string parameter) => Get(parameter, TypeSymbol.String).AsString;

    /// <summary>The value of a <c>number</c> parameter.</summary>
    public decimal GetNumber(string parameter) => Get(parameter, TypeSymbol.Number).AsDecimal;

    /// <summary>
    /// The value of a <c>duration</c> parameter, rounded to the nearest tick. A duration beyond <see cref="TimeSpan"/>'s
    /// range clamps, with an <see cref="RuntimeWarningKind.Overflow"/> warning.
    /// </summary>
    public TimeSpan GetDuration(string parameter) => Durations.ToTimeSpan(Get(parameter, TypeSymbol.Duration).AsDecimal, location, warn);

    /// <summary>The actor ID of an <c>actor</c> parameter.</summary>
    public string GetActor(string parameter) => Get(parameter, TypeSymbol.Actor).AsSymbol.Name;

    /// <summary>The node's current name, for a <c>node</c> parameter.</summary>
    public string GetNode(string parameter) => Get(parameter, TypeSymbol.Node).AsString;

    /// <summary>The member's name, for a parameter whose type is an enum.</summary>
    public string GetEnum(string parameter)
    {
        Value value = Find(parameter);
        return value.Type is EnumSymbol ? value.AsSymbol.Name : throw WrongAccessor(parameter, value.Type);
    }

    /// <summary>The member of a parameter whose type is an enum, as the host's own enum with the same member names.</summary>
    /// <typeparam name="TEnum">The host's enum.</typeparam>
    /// <exception cref="InvalidOperationException">The host's enum has no member with that name.</exception>
    public TEnum GetEnum<TEnum>(string parameter)
        where TEnum : struct, Enum
    {
        string member = GetEnum(parameter);
        return Enum.TryParse(member, ignoreCase: false, out TEnum result)
            ? result
            : throw new InvalidOperationException($"`{typeof(TEnum).Name}` has no member called `{member}`, which `{parameter}` can be.");
    }

    private Value Get(string parameter, TypeSymbol type)
    {
        Value value = Find(parameter);
        return value.Type == type ? value : throw WrongAccessor(parameter, value.Type);
    }

    private Value Find(string parameter)
    {
        int index = Symbol.Parameters.ToList().FindIndex(candidate => candidate.Name == parameter);
        return index >= 0 ? Arguments[index] : throw new ArgumentException($"`{Name}` has no parameter called `{parameter}`.", nameof(parameter));
    }

    private InvalidOperationException WrongAccessor(string parameter, TypeSymbol type) =>
        new($"`{parameter}` of `{Name}` is {type.Describe()}, so it can't be read this way.");
}
