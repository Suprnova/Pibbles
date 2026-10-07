using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Runtime;

/// <summary>
/// Evaluates lowered expressions with the semantics in <c>docs/language/reference.md</c>. The binder has already
/// checked every operand's type, so an operator never meets a type it doesn't work on. Evaluating never throws for
/// content: division by zero and overflow warn and carry on. It changes nothing except through the context's host functions.
/// </summary>
internal static class Evaluator
{
    public static Value Evaluate(Expr expression, IEvaluationContext context) => expression switch
    {
        NumberExpr number => Value.Number(number.Value),
        DurationExpr duration => Value.Duration(duration.Seconds),
        StringExpr text => Value.String(text.Value),
        BoolExpr boolean => Value.Bool(boolean.Value),
        EnumMemberExpr member => Value.Member(member.Member, member.EnumType),
        ActorExpr actor => Value.Actor(actor.Actor),
        NodeExpr node => Value.Node(node.Node),
        VariableExpr variable => context.GetVariable(variable.Variable),
        CallExpr call => context.CallFunction(call, [.. call.Arguments.Select(argument => Evaluate(argument, context))]),
        VisitsExpr visits => Value.Number(context.GetVisits(Evaluate(visits.Target, context).AsString)),
        ToDurationExpr conversion => Value.Duration(Evaluate(conversion.Operand, context).AsDecimal),
        UnaryExpr unary => EvaluateUnary(unary, context),
        BinaryExpr binary => EvaluateBinary(binary, context),
        _ => throw new NotSupportedException(expression.GetType().Name),
    };

    private static Value EvaluateUnary(UnaryExpr unary, IEvaluationContext context)
    {
        Value operand = Evaluate(unary.Operand, context);
        return unary.Operator is UnaryOperator.Not ? Value.Bool(!operand.AsBool) : Value.Numeric(unary.Type, -operand.AsDecimal);
    }

    private static Value EvaluateBinary(BinaryExpr binary, IEvaluationContext context)
    {
        if (binary.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            bool left = Evaluate(binary.Left, context).AsBool;
            bool decided = binary.Operator is BinaryOperator.Or ? left : !left;
            return decided ? Value.Bool(left) : Value.Bool(Evaluate(binary.Right, context).AsBool);
        }

        Value l = Evaluate(binary.Left, context);
        Value r = Evaluate(binary.Right, context);
        return binary.Operator switch
        {
            BinaryOperator.Equals => Value.Bool(l == r),
            BinaryOperator.NotEquals => Value.Bool(l != r),
            BinaryOperator.Less => Value.Bool(l.AsDecimal < r.AsDecimal),
            BinaryOperator.LessOrEqual => Value.Bool(l.AsDecimal <= r.AsDecimal),
            BinaryOperator.Greater => Value.Bool(l.AsDecimal > r.AsDecimal),
            BinaryOperator.GreaterOrEqual => Value.Bool(l.AsDecimal >= r.AsDecimal),
            BinaryOperator.Add when l.Type == Semantics.TypeSymbol.String => Value.String(l.AsString + r.AsString),
            _ => Value.Numeric(binary.Type, Arithmetic(binary, l.AsDecimal, r.AsDecimal, context)),
        };
    }

    private static decimal Arithmetic(BinaryExpr binary, decimal left, decimal right, IEvaluationContext context)
    {
        if (binary.Operator is BinaryOperator.Divide or BinaryOperator.Remainder && right == 0)
        {
            Warn(context, binary.Location, RuntimeWarningKind.DivisionByZero, "I divided by zero here, so the result is 0.");
            return 0;
        }

        try
        {
            return binary.Operator switch
            {
                BinaryOperator.Add => left + right,
                BinaryOperator.Subtract => left - right,
                BinaryOperator.Multiply => left * right,
                BinaryOperator.Divide => left / right,
                _ => Floored(left % right, right),
            };
        }
        catch (OverflowException)
        {
            bool negative = binary.Operator is BinaryOperator.Add or BinaryOperator.Subtract ? left < 0 : (left < 0) != (right < 0);
            Warn(context, binary.Location, RuntimeWarningKind.Overflow, negative
                ? "This result is too small for me to hold, so I used the smallest number I can."
                : "This result is too large for me to hold, so I used the largest number I can.");
            return negative ? decimal.MinValue : decimal.MaxValue;
        }
    }

    /// <summary>Gives a truncated remainder the divisor's sign, as the language's <c>%</c> does.</summary>
    private static decimal Floored(decimal remainder, decimal divisor) => remainder != 0 && (remainder < 0) != (divisor < 0) ? remainder + divisor : remainder;

    private static void Warn(IEvaluationContext context, SourceLocation location, RuntimeWarningKind kind, string message) =>
        context.Warn(new(kind, message, location));
}
