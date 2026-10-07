using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// The arguments of a command or of a markup span, read by parameter name with an accessor for the parameter's type.
/// Every parameter has a value, since a default fills any argument the story left out.
/// </summary>
/// <remarks>
/// An accessor throws <see cref="ArgumentException"/> for a name the owner has no parameter for, and
/// <see cref="InvalidOperationException"/> if it isn't the accessor for that parameter's type: both are the host's mistakes.
/// Two <see cref="Arguments"/> are equal when they hold the same values for the same owner.
/// </remarks>
public sealed class Arguments : IEquatable<Arguments>
{
    private readonly string owner;
    private readonly IReadOnlyList<ParameterSymbol> parameters;
    private readonly SourceLocation location;
    private readonly Action<RuntimeWarning> warn;

    internal Arguments(string owner, IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<Value> values, SourceLocation location, Action<RuntimeWarning> warn)
    {
        this.owner = owner;
        this.parameters = parameters;
        Values = values;
        this.location = location;
        this.warn = warn;
    }

    /// <summary>The arguments, in the order of the owner's parameters.</summary>
    internal IReadOnlyList<Value> Values { get; }

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

    /// <inheritdoc/>
    public bool Equals(Arguments? other) => other is not null && owner == other.owner && Values.SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Arguments);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(owner, Values.Count);

    private Value Get(string parameter, TypeSymbol type)
    {
        Value value = Find(parameter);
        return value.Type == type ? value : throw WrongAccessor(parameter, value.Type);
    }

    private Value Find(string parameter)
    {
        int index = parameters.ToList().FindIndex(candidate => candidate.Name == parameter);
        return index >= 0 ? Values[index] : throw new ArgumentException($"{owner} has no parameter called `{parameter}`.", nameof(parameter));
    }

    private InvalidOperationException WrongAccessor(string parameter, TypeSymbol type) =>
        new($"`{parameter}` of {owner} is {type.Describe()}, so it can't be read this way.");
}
