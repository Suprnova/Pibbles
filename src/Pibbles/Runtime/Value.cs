using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// A value of one of the language's types. Numbers are exact decimals, durations are decimal seconds, and enum members,
/// actors and nodes are held by identity (a node by its current name). Create one with the factory methods; the default
/// value has no type and isn't valid.
/// </summary>
internal readonly struct Value : IEquatable<Value>
{
    private readonly decimal number;
    private readonly string? text;
    private readonly Symbol? symbol;

    private Value(TypeSymbol type, decimal number = 0, string? text = null, Symbol? symbol = null)
    {
        Type = type;
        this.number = number;
        this.text = text;
        this.symbol = symbol;
    }

    /// <summary>The value's type: a built-in type, or the enum a member belongs to.</summary>
    public TypeSymbol Type { get; }

    /// <summary>The value of a <c>bool</c>.</summary>
    public bool AsBool => number != 0;

    /// <summary>The value of a <c>number</c>, or of a <c>duration</c> in seconds.</summary>
    public decimal AsDecimal => number;

    /// <summary>The value of a <c>string</c>, or a node's current name.</summary>
    public string AsString => text!;

    /// <summary>The enum member or actor.</summary>
    public Symbol AsSymbol => symbol!;

    public static Value Bool(bool value) => new(TypeSymbol.Bool, value ? 1 : 0);

    public static Value Number(decimal value) => new(TypeSymbol.Number, value);

    public static Value Duration(decimal seconds) => new(TypeSymbol.Duration, seconds);

    public static Value String(string value) => new(TypeSymbol.String, text: value);

    public static Value Member(EnumMemberSymbol member, TypeSymbol enumType) => new(enumType, symbol: member);

    public static Value Actor(ActorSymbol actor) => new(TypeSymbol.Actor, symbol: actor);

    public static Value Node(string name) => new(TypeSymbol.Node, text: name);

    /// <summary>A <c>number</c> or a <c>duration</c>, whichever <paramref name="type"/> is.</summary>
    public static Value Numeric(TypeSymbol type, decimal value) => type == TypeSymbol.Duration ? Duration(value) : Number(value);

    /// <summary>
    /// Whether two values are equal: the same type, and the same number (so <c>1.50</c> equals <c>1.5</c>), the same
    /// characters, or the same enum member, actor or node.
    /// </summary>
    public bool Equals(Value other) =>
        Type == other.Type && number == other.number && string.Equals(text, other.text, StringComparison.Ordinal) && ReferenceEquals(symbol, other.symbol);

    public override bool Equals(object? obj) => obj is Value other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, number, text, symbol);

    public static bool operator ==(Value left, Value right) => left.Equals(right);

    public static bool operator !=(Value left, Value right) => !left.Equals(right);
}
