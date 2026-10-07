using System.Globalization;
using System.Text;
using Pibbles.Compiler;
using Pibbles.Syntax;

namespace Pibbles.Tests.Compiler;

/// <summary>
/// Writes a compiled story as text for snapshots: its variables and aliases, each node's instructions with their
/// indexes, then its templates and where each ID lives. An ID the compiler made up is written as it is, <c>~path:line</c>;
/// a written one gets a <c>#</c>.
/// </summary>
internal sealed class IrDump(Story story, ISet<Type>? kinds)
{
    private readonly StringBuilder builder = new();

    public static string Write(Story story, ISet<Type>? kinds = null) => new IrDump(story, kinds).Write();

    private string Write()
    {
        foreach (StoryVariable variable in story.Variables)
            builder.Append(CultureInfo.InvariantCulture, $"var ${variable.Variable.Name} = {Format(variable.StartingValue)}\n");

        foreach ((string old, string current) in story.Aliases.OrderBy(alias => alias.Key, StringComparer.Ordinal))
            builder.Append(CultureInfo.InvariantCulture, $"alias {old} -> {current}\n");

        foreach (CompiledNode node in story.CompiledNodes.Values)
        {
            builder.Append(CultureInfo.InvariantCulture, $"node {node.Name}\n");
            for (int i = 0; i < node.Instructions.Count; i++)
                builder.Append(CultureInfo.InvariantCulture, $"  {i,3}  {Format(node.Instructions[i])}\n");
        }

        builder.Append("templates\n");
        foreach ((string id, Template template) in story.Templates)
            builder.Append(CultureInfo.InvariantCulture, $"  {Id(id)}: {Format(template)}\n");

        builder.Append("ids\n");
        foreach ((string id, IdSite site) in story.Sites)
            builder.Append(CultureInfo.InvariantCulture, $"  {Id(id)} -> {site.Node}[{site.Index}]\n");

        return builder.ToString();
    }

    private string Id(string id) => story.FallbackIds.Contains(id) ? id : $"#{id}";

    private string Format(Instruction instruction)
    {
        kinds?.Add(instruction.GetType());
        return instruction switch
        {
            LineInstruction line => $"Line {Id(line.Id)}",
            PoseInstruction pose => $"Pose {pose.Actor.Name} {pose.Pose.Name}",
            ChoiceInstruction choice => $"Choice [{string.Join(", ", choice.Options.Select(Format))}] join {choice.Join}",
            BranchInstruction branch => $"Branch -> {branch.Target}",
            BranchIfFalseInstruction branch => $"BranchIfFalse {Format(branch.Condition)} -> {branch.Target}",
            SetInstruction set => $"Set ${set.Variable.Name} = {Format(set.Value)}",
            WaitInstruction wait => $"Wait {Format(wait.Duration)}",
            CommandInstruction command => $"Command @{command.Command.Name}({string.Join(", ", command.Arguments.Select(Format))}) waits={Lower(command.Waits)}",
            JumpInstruction jump => $"Jump {jump.Node}",
            CallInstruction call => $"Call {call.Node} {Id(call.Id)}",
            ReturnInstruction => "Return",
            EndInstruction => "End",
            VariationInstruction variation => $"Variation {variation.Kind} {Id(variation.BlockId)} [{string.Join(", ", variation.Alternatives)}] exit {variation.Exit}",
            _ => throw new NotSupportedException(instruction.GetType().Name),
        };
    }

    private string Format(CompiledOption option) =>
        $"{Id(option.Id)}{(option.Condition is { } condition ? $" if {Format(condition)}" : "")}{(option.IsOnce ? " once" : "")} -> {option.Body}";

    private string Format(Template template)
    {
        string speaker = template.Speaker?.Name ?? "-";
        string tags = string.Join(", ", template.Tags.Select(tag => tag.Name + (tag.Value is null ? "" : $":{tag.Value}") + (tag.Member is { } member ? $"({member.Name})" : "")));
        return $"{speaker} <{tags}> {Format(template.Content)}";
    }

    private string Format(IEnumerable<TemplateElement> content) => string.Concat(content.Select(Format));

    private string Format(TemplateElement element)
    {
        kinds?.Add(element.GetType());
        return element switch
        {
            TextElement text => $"\"{text.Text}\"",
            MarkupElement markup => $"[{markup.Markup.Name}{string.Concat(markup.Arguments.Select(argument => " " + Format(argument)))}]({Format(markup.Children)})",
            InterpolationElement interpolation => $"{{{Format(interpolation.Value)}:{interpolation.Value.Type.Name}}}",
            CommandElement command => $"{{@{command.Command.Name}({string.Join(", ", command.Arguments.Select(Format))}) waits={Lower(command.Waits)}}}",
            InputWaitElement => "{w}",
            PauseElement pause => $"{{w {Format(pause.Duration)}}}",
            SpeedElement speed => $"{{speed {Format(speed.Factor)}}}",
            SpeedResetElement => "{speed}",
            PageBreakElement => "{p}",
            LineBreakElement => "{br}",
            IconElement icon => $"{{icon {icon.Icon.Name}}}",
            ConditionalElement conditional => FormatConditional(conditional),
            _ => throw new NotSupportedException(element.GetType().Name),
        };
    }

    private string FormatConditional(ConditionalElement conditional)
    {
        IEnumerable<string> branches = conditional.Branches.Select((branch, index) => $"{(index == 0 ? "if" : "elif")} {Format(branch.Condition)}: {Format(branch.Content)}");
        return "{" + string.Join(" | ", conditional.Else is { } @else ? [.. branches, $"else: {Format(@else)}"] : branches) + "}";
    }

    private string Format(Expr expression)
    {
        kinds?.Add(expression.GetType());
        return expression switch
        {
            NumberExpr number => number.Value.ToString(CultureInfo.InvariantCulture),
            DurationExpr duration => duration.Seconds.ToString(CultureInfo.InvariantCulture) + "s",
            StringExpr text => $"\"{text.Value}\"",
            BoolExpr boolean => Lower(boolean.Value),
            EnumMemberExpr member => member.Member.Name,
            ActorExpr actor => actor.Actor.Name,
            NodeExpr node => $"node({node.Node})",
            VariableExpr variable => $"${variable.Variable.Name}",
            CallExpr call => $"{call.Function.Name}({string.Join(", ", call.Arguments.Select(Format))})",
            VisitsExpr visits => $"visits({Format(visits.Target)})",
            UnaryExpr unary => $"({(unary.Operator is UnaryOperator.Not ? "not " : "-")}{Format(unary.Operand)}):{unary.Type.Name}",
            BinaryExpr binary => $"({Format(binary.Left)} {Operator(binary.Operator)} {Format(binary.Right)}):{binary.Type.Name}",
            ToDurationExpr conversion => $"duration({Format(conversion.Operand)})",
            _ => throw new NotSupportedException(expression.GetType().Name),
        };
    }

    private static string Lower(bool value) => value ? "true" : "false";

    private static string Operator(BinaryOperator @operator) => @operator switch
    {
        BinaryOperator.Or => "or",
        BinaryOperator.And => "and",
        BinaryOperator.Equals => "==",
        BinaryOperator.NotEquals => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.Greater => ">",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        _ => "%",
    };
}
