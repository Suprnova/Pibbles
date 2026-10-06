using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Expressions: comparing with <c>true</c> or <c>false</c>, and <c>@set</c> that <c>+=</c> or <c>-=</c> says more briefly.</summary>
internal sealed partial class StyleChecks
{
    /// <summary>PIB5020: <c>x == true</c> is <c>x</c>, and <c>x == false</c> is <c>not x</c>, and likewise for <c>!=</c>.</summary>
    private void CheckComparisonWithBool(ExpressionSyntax expression)
    {
        if (expression is not BinaryExpressionSyntax { Operator: BinaryOperator.Equals or BinaryOperator.NotEquals } comparison)
            return;

        (BooleanLiteralSyntax? literal, ExpressionSyntax other) = (comparison.Left, comparison.Right) switch
        {
            (_, BooleanLiteralSyntax right) => (right, comparison.Left),
            (BooleanLiteralSyntax left, _) => (left, comparison.Right),
            _ => (null, comparison),
        };

        if (literal is null)
            return;

        string written = TextOf(other.Span);
        string condition = literal.Value == comparison.Operator is BinaryOperator.Equals
            ? written
            : other is BinaryExpressionSyntax ? $"not ({written})" : $"not {written}";
        Report(DiagnosticCatalog.ComparisonWithBool, comparison.Span, literal.Value ? "true" : "false", condition);
    }

    /// <summary>PIB5021: <c>@set $x = $x + v</c> is <c>@set $x += v</c>, and likewise for <c>-</c>.</summary>
    private void CheckCompoundAssignment(SetStatementSyntax set)
    {
        if (set is not { Operator: AssignmentOperator.Assign, Value: BinaryExpressionSyntax { Operator: BinaryOperator.Add or BinaryOperator.Subtract, Left: VariableExpressionSyntax left } value }
            || left.Name != set.Variable.Name)
            return;

        string @operator = value.Operator is BinaryOperator.Add ? "+=" : "-=";
        Report(DiagnosticCatalog.CompoundAssignment, set.Span, @operator, $"@set ${set.Variable.Name} {@operator} {TextOf(value.Right.Span)}");
    }
}
