using Pibbles.Compiler;
using Pibbles.Semantics;

namespace Pibbles.Cli.Transcripts;

/// <summary>Finds the host functions a story calls anywhere: in its code, its templates and its variables' starting values.</summary>
internal static class StoryCalls
{
    public static IEnumerable<FunctionSymbol> Functions(Story story)
    {
        IEnumerable<Expr> expressions = story.CompiledNodes.Values.SelectMany(node => node.Instructions).SelectMany(Of)
            .Concat(story.Templates.Values.SelectMany(template => template.Content).SelectMany(Of))
            .Concat(story.Variables.Select(variable => variable.StartingValue));

        return expressions.SelectMany(Calls).DistinctBy(function => function.Name);
    }

    private static IEnumerable<Expr> Of(Instruction instruction) => instruction switch
    {
        ChoiceInstruction choice => choice.Options.Select(option => option.Condition).OfType<Expr>(),
        BranchIfFalseInstruction branch => [branch.Condition],
        SetInstruction set => [set.Value],
        WaitInstruction wait => [wait.Duration],
        CommandInstruction command => command.Arguments,
        _ => [],
    };

    private static IEnumerable<Expr> Of(TemplateElement element) => element switch
    {
        MarkupElement markup => [.. markup.Arguments, .. markup.Children.SelectMany(Of)],
        InterpolationElement interpolation => [interpolation.Value],
        CommandElement command => command.Arguments,
        PauseElement pause => [pause.Duration],
        SpeedElement speed => [speed.Factor],
        ConditionalElement conditional => [.. conditional.Branches.SelectMany(branch => (IEnumerable<Expr>)[branch.Condition, .. branch.Content.SelectMany(Of)]), .. (conditional.Else ?? []).SelectMany(Of)],
        _ => [],
    };

    private static IEnumerable<FunctionSymbol> Calls(Expr expression) => expression switch
    {
        CallExpr call => [call.Function, .. call.Arguments.SelectMany(Calls)],
        VisitsExpr visits => Calls(visits.Target),
        UnaryExpr unary => Calls(unary.Operand),
        BinaryExpr binary => [.. Calls(binary.Left), .. Calls(binary.Right)],
        ToDurationExpr conversion => Calls(conversion.Operand),
        _ => [],
    };
}
