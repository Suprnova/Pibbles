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
    [InlineData("\"a\" == \"a\"")]
    public void FoldCondition_ConditionThatCanChange_GivesNothing(string condition) =>
        Assert.Null(FoldCondition(condition));

    private static bool? FoldCondition(string condition)
    {
        var tree = SyntaxTree.Parse(new SourceText("story.pib", $"== a.b\n@if {condition}\n    Hi.\n"));

        return ConstantValue.FoldCondition(((IfStatementSyntax)tree.Root.Nodes[0].Body[0]).Condition);
    }
}
