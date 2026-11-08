using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public class ConstantValueTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("(false)", false)]
    [InlineData("not true", false)]
    [InlineData("not not true", true)]
    [InlineData("1 > 2", false)]
    [InlineData("1 == 1", true)]
    [InlineData("1 != 1", false)]
    [InlineData("1s < 2s", true)]
    [InlineData("1 + 1 == 2", true)]
    [InlineData("7 % 3 == 1", true)]
    [InlineData("-7 % 3 == 2", true)]
    [InlineData("7 % -3 == -2", true)]
    [InlineData("true == true", true)]
    [InlineData("true != false", true)]
    [InlineData("true and false", false)]
    [InlineData("false or true", true)]
    [InlineData("false and $x", false)]
    [InlineData("true or $x", true)]
    [InlineData("1 > 2 and f(1)", false)]
    [InlineData("$x and false", false)]
    [InlineData("$x or true", true)]
    [InlineData("f(1) > 2 and (1 > 2)", false)]
    public void FoldCondition_ConstantCondition_GivesItsValue(string condition, bool expected) =>
        Assert.Equal(expected, FoldCondition(condition));

    [Theory]
    [InlineData("$x")]
    [InlineData("not $x")]
    [InlineData("$x > 1")]
    [InlineData("true and $x")]
    [InlineData("false or $x")]
    [InlineData("f(1)")]
    [InlineData("1 / 0 > 1")]
    [InlineData("1 % 0 > 1")]
    [InlineData("\"a\" == \"a\"")]
    public void FoldCondition_ConditionThatCanChange_GivesNothing(string condition) =>
        Assert.Null(FoldCondition(condition));

    [Theory]
    [InlineData("99999999999999999999999999 * 99999999999999999999999999")]
    [InlineData("79228162514264337593543950335 + 1")]
    [InlineData("-79228162514264337593543950335 - 1")]
    [InlineData("79228162514264337593543950335 / 0.1")]
    [InlineData("(1 + 1) * 79228162514264337593543950335")]
    [InlineData("79228162514264337593543950335s + 1s")]
    public void Overflows_ConstantOperationTooLargeToHold_IsTrue(string operation) =>
        Assert.True(Overflows(operation));

    [Theory]
    [InlineData("79228162514264337593543950335 - 1")]
    [InlineData("79228162514264337593543950335 * 0")]
    [InlineData("79228162514264337593543950335 / 1")]
    [InlineData("79228162514264337593543950335 % 3")]
    [InlineData("79228162514264337593543950335 / 0")]
    [InlineData("79228162514264337593543950335 % 0")]
    [InlineData("$x * 99999999999999999999999999")]
    [InlineData("f(1) + 79228162514264337593543950335")]
    [InlineData("(99999999999999999999999999 * 99999999999999999999999999) * 2")]
    public void Overflows_OperationThatFitsOrIsNotConstant_IsFalse(string operation) =>
        Assert.False(Overflows(operation));

    private static bool Overflows(string operation)
    {
        var tree = SyntaxTree.Parse(new SourceText("story.pib", $"== a.b\n@if {operation} > 0\n    Hi.\n"));
        var comparison = (BinaryExpressionSyntax)((IfStatementSyntax)tree.Root.Nodes[0].Body[0]).Condition;

        return ConstantValue.Overflows((BinaryExpressionSyntax)comparison.Left);
    }

    private static bool? FoldCondition(string condition)
    {
        var tree = SyntaxTree.Parse(new SourceText("story.pib", $"== a.b\n@if {condition}\n    Hi.\n"));

        return ConstantValue.FoldCondition(((IfStatementSyntax)tree.Root.Nodes[0].Body[0]).Condition);
    }
}
