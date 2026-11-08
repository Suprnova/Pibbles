using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Folds expressions made only of literals, unary minus, <c>+ - * / %</c> and parentheses into one value, in
/// <see cref="decimal"/> so that sums like <c>0.3s - 0.1s - 0.2s</c> are exact. Anything else, and anything that
/// overflows or divides by zero, has no constant value. <see cref="FoldCondition"/> does the same for conditions.
/// </summary>
internal static class ConstantValue
{
    /// <summary>
    /// Determines if the provided <paramref name="expression"/> is constant, and returns the value if so.
    /// </summary>
    /// <param name="expression">The expression to fold.</param>
    /// <returns>
    /// The value of <paramref name="expression"/> if it's constant, or <see langword="null"/> if it's not.
    /// </returns>
    public static decimal? Fold(ExpressionSyntax expression) => expression switch
    {
        NumberLiteralSyntax number => number.Value,
        DurationLiteralSyntax duration => duration.Seconds,
        ParenthesizedExpressionSyntax parenthesized => Fold(parenthesized.Expression),
        UnaryExpressionSyntax { Operator: UnaryOperator.Negate } unary => -Fold(unary.Operand),
        BinaryExpressionSyntax binary when Fold(binary.Left) is { } left && Fold(binary.Right) is { } right => Apply(binary.Operator, left, right),
        _ => null,
    };

    /// <summary>
    /// Determines if the provided boolean <paramref name="condition"/> is constant, and returns the value if so.
    /// </summary>
    /// <param name="condition">The condition to fold.</param>
    /// <returns>
    /// <c>true</c> if the condition is always true, <c>false</c> if it's always false, or <see langword="null"/> if it can change.
    /// </returns>
    public static bool? FoldCondition(ExpressionSyntax condition) => condition switch
    {
        BooleanLiteralSyntax boolean => boolean.Value,
        ParenthesizedExpressionSyntax parenthesized => FoldCondition(parenthesized.Expression),
        UnaryExpressionSyntax { Operator: UnaryOperator.Not } unary => !FoldCondition(unary.Operand),
        BinaryExpressionSyntax { Operator: BinaryOperator.And } and => (FoldCondition(and.Left), FoldCondition(and.Right)) switch { (false, _) or (_, false) => false, (true, { } right) => right, _ => null },
        BinaryExpressionSyntax { Operator: BinaryOperator.Or } or => (FoldCondition(or.Left), FoldCondition(or.Right)) switch { (true, _) or (_, true) => true, (false, { } right) => right, _ => null },
        BinaryExpressionSyntax binary when Fold(binary.Left) is { } left && Fold(binary.Right) is { } right => Compare(binary.Operator, left, right),
        BinaryExpressionSyntax { Operator: BinaryOperator.Equals or BinaryOperator.NotEquals } binary when FoldCondition(binary.Left) is { } left && FoldCondition(binary.Right) is { } right =>
            (left == right) == (binary.Operator is BinaryOperator.Equals),
        _ => null,
    };

    /// <summary>Adds two values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The sum, or <see langword="null"/> if it's too large for a <see cref="decimal"/>.</returns>
    public static decimal? Add(decimal left, decimal right) => Apply(BinaryOperator.Add, left, right);

    private static bool? Compare(BinaryOperator @operator, decimal left, decimal right) => @operator switch
    {
        BinaryOperator.Equals => left == right,
        BinaryOperator.NotEquals => left != right,
        BinaryOperator.Less => left < right,
        BinaryOperator.LessOrEqual => left <= right,
        BinaryOperator.Greater => left > right,
        BinaryOperator.GreaterOrEqual => left >= right,
        _ => null,
    };

    private static decimal Floored(decimal remainder, decimal divisor) => remainder != 0 && (remainder < 0) != (divisor < 0) ? remainder + divisor : remainder;

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
                BinaryOperator.Remainder when right != 0 => Floored(left % right, right),
                _ => null,
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
