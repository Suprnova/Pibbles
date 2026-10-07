using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Folds expressions made only of literals, unary minus, <c>+ - * /</c> and parentheses into one value, in
/// <see cref="decimal"/> so that sums like <c>0.3s - 0.1s - 0.2s</c> are exact. Anything else, and anything that
/// can't be represented or divides by zero, has no constant value.
/// </summary>
internal static class ConstantValue
{
    /// <summary>The value of <paramref name="expression"/> if it's constant, in seconds for a duration, or <see langword="null"/>.</summary>
    public static decimal? Fold(ExpressionSyntax expression) => expression switch
    {
        NumberLiteralSyntax number => FromDouble(number.Value),
        DurationLiteralSyntax duration => FromDouble(duration.Seconds),
        ParenthesizedExpressionSyntax parenthesized => Fold(parenthesized.Expression),
        UnaryExpressionSyntax { Operator: UnaryOperator.Negate } unary => -Fold(unary.Operand),
        BinaryExpressionSyntax binary when Fold(binary.Left) is { } left && Fold(binary.Right) is { } right => Apply(binary.Operator, left, right),
        _ => null,
    };

    /// <summary>The value of a literal, or <see langword="null"/> if it's too large for a <see cref="decimal"/>.</summary>
    public static decimal? FromDouble(double value) => Math.Abs(value) < (double)decimal.MaxValue ? (decimal)value : null;

    /// <summary>The sum of two values, or <see langword="null"/> if it's too large for a <see cref="decimal"/>.</summary>
    public static decimal? Add(decimal left, decimal right) => Apply(BinaryOperator.Add, left, right);

    private static decimal? Apply(BinaryOperator @operator, decimal left, decimal right)
    {
        try
        {
            return @operator switch
            {
                BinaryOperator.Add => left + right,
                BinaryOperator.Subtract => left - right,
                BinaryOperator.Multiply => left * right,
                BinaryOperator.Divide when right != 0 => left / right,
                _ => null,
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
