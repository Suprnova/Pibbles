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
/// Two invocations are equal when they are for the same command with the same values.
/// </remarks>
public sealed class CommandInvocation : IEquatable<CommandInvocation>
{
    private readonly Arguments arguments;

    internal CommandInvocation(CommandSymbol command, IReadOnlyList<Value> values, SourceLocation location, Action<RuntimeWarning> warn)
    {
        Symbol = command;
        arguments = new($"`{command.Name}`", command.Parameters, values, location, warn);
    }

    /// <summary>The command's name, without the <c>@</c>.</summary>
    public string Name => Symbol.Name;

    internal CommandSymbol Symbol { get; }

    /// <summary>The arguments, in the order of the command's parameters.</summary>
    internal IReadOnlyList<Value> Values => arguments.Values;

    /// <inheritdoc cref="Arguments.GetBool"/>
    public bool GetBool(string parameter) => arguments.GetBool(parameter);

    /// <inheritdoc cref="Arguments.GetString"/>
    public string GetString(string parameter) => arguments.GetString(parameter);

    /// <inheritdoc cref="Arguments.GetNumber"/>
    public decimal GetNumber(string parameter) => arguments.GetNumber(parameter);

    /// <inheritdoc cref="Arguments.GetDuration"/>
    public TimeSpan GetDuration(string parameter) => arguments.GetDuration(parameter);

    /// <inheritdoc cref="Arguments.GetActor"/>
    public string GetActor(string parameter) => arguments.GetActor(parameter);

    /// <inheritdoc cref="Arguments.GetNode"/>
    public string GetNode(string parameter) => arguments.GetNode(parameter);

    /// <inheritdoc cref="Arguments.GetEnum(string)"/>
    public string GetEnum(string parameter) => arguments.GetEnum(parameter);

    /// <inheritdoc cref="Arguments.GetEnum{TEnum}"/>
    public TEnum GetEnum<TEnum>(string parameter)
        where TEnum : struct, Enum => arguments.GetEnum<TEnum>(parameter);

    /// <inheritdoc/>
    public bool Equals(CommandInvocation? other) => other is not null && ReferenceEquals(Symbol, other.Symbol) && arguments.Equals(other.arguments);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CommandInvocation);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Symbol, arguments);
}
