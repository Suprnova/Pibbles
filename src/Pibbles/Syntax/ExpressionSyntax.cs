namespace Pibbles.Syntax;

/// <summary>An expression: a value the story computes, in a condition, an assignment or an argument.</summary>
public abstract record ExpressionSyntax : SyntaxNode;

/// <summary>A number, such as <c>3</c>, <c>0.5</c> or <c>.5</c>.</summary>
/// <param name="Value">The number's value.</param>
public sealed record NumberLiteralSyntax(double Value) : ExpressionSyntax;

/// <summary>A duration, such as <c>0.5s</c> or <c>300ms</c>.</summary>
/// <param name="Seconds">The duration in seconds, so <c>300ms</c> is <c>0.3</c>.</param>
public sealed record DurationLiteralSyntax(double Seconds) : ExpressionSyntax;

/// <summary>Quoted text, such as <c>"crowbar"</c>.</summary>
/// <param name="Value">The text between the quotes, with escapes resolved.</param>
public sealed record StringLiteralSyntax(string Value) : ExpressionSyntax;

/// <summary><c>true</c> or <c>false</c>.</summary>
/// <param name="Value">The value.</param>
public sealed record BooleanLiteralSyntax(bool Value) : ExpressionSyntax;

/// <summary>A variable, such as <c>$has_key</c>.</summary>
/// <param name="Name">The variable's name, without the <c>$</c>.</param>
public sealed record VariableExpressionSyntax(string Name) : ExpressionSyntax;

/// <summary>
/// A bare name: an enum member, an actor or a node, such as <c>left</c>, <c>mira</c> or <c>kitchen.door</c>.
/// Which one it is depends on the type expected where it appears.
/// </summary>
/// <param name="Name">The name as written.</param>
public sealed record NameExpressionSyntax(string Name) : ExpressionSyntax;

/// <summary>A call to a host function or a built-in one, such as <c>has_item("key")</c> or <c>visits(kitchen.door)</c>.</summary>
/// <param name="Function">The function's name.</param>
/// <param name="Arguments">The arguments, in order.</param>
public sealed record CallExpressionSyntax(NameSyntax Function, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax;

/// <summary>An expression in parentheses.</summary>
/// <param name="Expression">The expression inside.</param>
public sealed record ParenthesizedExpressionSyntax(ExpressionSyntax Expression) : ExpressionSyntax;

/// <summary>An operator before one operand: <c>not</c> or <c>-</c>.</summary>
/// <param name="Operator">The operator.</param>
/// <param name="OperatorSpan">Where the operator is written.</param>
/// <param name="Operand">The operand.</param>
public sealed record UnaryExpressionSyntax(UnaryOperator Operator, TextSpan OperatorSpan, ExpressionSyntax Operand) : ExpressionSyntax;

/// <summary>An operator between two operands, such as <c>$bravery + 1</c> or <c>$a and $b</c>.</summary>
/// <param name="Left">The left operand.</param>
/// <param name="Operator">The operator.</param>
/// <param name="OperatorSpan">Where the operator is written.</param>
/// <param name="Right">The right operand.</param>
public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, BinaryOperator Operator, TextSpan OperatorSpan, ExpressionSyntax Right) : ExpressionSyntax;

/// <summary>
/// An expression that couldn't be read: a value the source leaves out, which has a zero-length span, or a malformed
/// number. Its problem has already been reported.
/// </summary>
public sealed record ErrorExpressionSyntax : ExpressionSyntax;

/// <summary>The operators that come before one operand.</summary>
public enum UnaryOperator
{
    /// <summary><c>not</c></summary>
    Not,

    /// <summary><c>-</c></summary>
    Negate,
}

/// <summary>The operators that come between two operands.</summary>
public enum BinaryOperator
{
    /// <summary><c>or</c></summary>
    Or,

    /// <summary><c>and</c></summary>
    And,

    /// <summary><c>==</c></summary>
    Equals,

    /// <summary><c>!=</c></summary>
    NotEquals,

    /// <summary><c>&lt;</c></summary>
    Less,

    /// <summary><c>&lt;=</c></summary>
    LessOrEqual,

    /// <summary><c>&gt;</c></summary>
    Greater,

    /// <summary><c>&gt;=</c></summary>
    GreaterOrEqual,

    /// <summary><c>+</c></summary>
    Add,

    /// <summary><c>-</c></summary>
    Subtract,

    /// <summary><c>*</c></summary>
    Multiply,

    /// <summary><c>/</c></summary>
    Divide,

    /// <summary><c>%</c></summary>
    Remainder,
}
