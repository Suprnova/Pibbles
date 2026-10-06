using System.Globalization;
using System.Text;
using Pibbles.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Prints a syntax tree back to source that parses to the same tree. It writes one canonical form: four-space
/// indentation, single spaces between tokens, and an escape before every character that could be read as markup, a
/// point, a tag, a modifier or a speaker's colon.
/// </summary>
/// <remarks>
/// Comments, when given, are mixed in where they're allowed: after the content of every other <c>@</c> line or header,
/// and on lines of their own, at assorted indentation, after every third line. Any left over end the file.
/// </remarks>
internal sealed class SyntaxPrinter(IEnumerable<string> comments)
{
    private readonly StringBuilder builder = new();
    private readonly Queue<string> comments = new(comments);
    private int lines;

    public static string Print(FileSyntax file) => Print(file, []);

    public static string Print(FileSyntax file, IEnumerable<string> comments)
    {
        var printer = new SyntaxPrinter(comments);
        printer.File(file);
        while (printer.comments.Count > 0)
            printer.CommentLine();

        return printer.builder.ToString();
    }

    private void Line(int depth, string text)
    {
        lines++;
        builder.Append(' ', depth * 4).Append(text);
        if (lines % 2 == 0 && text is ['@', ..] or ['=', '=', ..] && comments.TryDequeue(out string? trailing))
            builder.Append(" // ").Append(trailing);

        builder.Append('\n');
        if (lines % 3 == 0 && comments.Count > 0)
            CommentLine();
    }

    private void CommentLine() => builder.Append(' ', lines % 5).Append("// ").Append(comments.Dequeue()).Append('\n');

    private void File(FileSyntax file)
    {
        if (file.Prefix is { } prefix)
            Line(0, $"@prefix {prefix.Name.Text}");

        foreach (DeclarationSyntax declaration in file.Declarations)
            Declaration(declaration);

        foreach (NodeSyntax node in file.Nodes)
        {
            Line(0, $"== {node.Name.Text}{string.Concat(node.Aliases.Select(alias => $" #was:{alias.Text}"))}");
            Statements(node.Body, 0);
        }
    }

    private void Declaration(DeclarationSyntax declaration)
    {
        switch (declaration)
        {
            case ActorDeclarationSyntax actor:
                Line(0, $"@actor {actor.Name.Text}");
                if (actor.DisplayName is not null)
                    Line(1, $"name: {actor.DisplayName}");
                if (actor.Poses.Count > 0)
                    Line(1, $"poses: {Names(actor.Poses)}");
                break;

            case EnumDeclarationSyntax @enum:
                Line(0, $"@enum {@enum.Name.Text}: {Names(@enum.Members)}");
                break;

            case VariableDeclarationSyntax variable:
                string type = variable.Type is null ? "" : $": {variable.Type.Text}";
                Line(0, $"@var ${variable.Variable.Name}{type} = {Expression(variable.Value)}");
                break;

            case CommandDeclarationSyntax command:
                Line(0, $"@command {command.Name.Text}({Parameters(command.Parameters)}){(command.IsInline ? " inline" : "")}{(command.Waits ? " waits" : "")}");
                break;

            case MarkupDeclarationSyntax markup:
                Line(0, markup.Parameters.Count > 0 ? $"@markup {markup.Name.Text}({Parameters(markup.Parameters)})" : $"@markup {markup.Name.Text}");
                break;

            case IconDeclarationSyntax icon:
                Line(0, $"@icon {Names(icon.Names)}");
                break;

            case TagDeclarationSyntax tag:
                Line(0, $"@tag {string.Join(", ", tag.Entries.Select(entry => entry.Type is null ? entry.Name.Text : $"{entry.Name.Text}: {entry.Type.Text}{(entry.AllowsEmpty ? "?" : "")}"))}");
                break;

            case FunctionDeclarationSyntax function:
                Line(0, $"@function {function.Name.Text}({Parameters(function.Parameters)}) -> {function.ReturnType.Text}");
                break;
        }
    }

    private static string Names(IEnumerable<NameSyntax> names) => string.Join(", ", names.Select(name => name.Text));

    private static string Parameters(IEnumerable<ParameterSyntax> parameters) =>
        string.Join(", ", parameters.Select(parameter => $"{parameter.Name.Text}: {parameter.Type.Text}{(parameter.Default is null ? "" : $" = {Expression(parameter.Default)}")}"));

    private void Statements(IEnumerable<StatementSyntax> statements, int depth)
    {
        foreach (StatementSyntax statement in statements)
            Statement(statement, depth);
    }

    private void Statement(StatementSyntax statement, int depth)
    {
        if (SingleLine(statement) is { } line)
        {
            Line(depth, line);
            return;
        }

        switch (statement)
        {
            case ChoiceSyntax choice:
                foreach (OptionSyntax option in choice.Options)
                {
                    Line(depth, Option(option));
                    Statements(option.Body, depth + 1);
                }
                break;

            case IfStatementSyntax @if:
                Line(depth, $"@if {Expression(@if.Condition)}");
                Statements(@if.Body, depth + 1);
                foreach (ElseIfClauseSyntax elseIf in @if.ElseIfs)
                {
                    Line(depth, $"@elif {Expression(elseIf.Condition)}");
                    Statements(elseIf.Body, depth + 1);
                }
                if (@if.Else is { } @else)
                {
                    Line(depth, "@else");
                    Statements(@else.Body, depth + 1);
                }
                break;

            case VariationStatementSyntax variation:
                Line(depth, $"{(variation.Kind is VariationKind.Sequence ? "@sequence" : "@cycle")}{Tags(variation.Tags)}");
                foreach (AlternativeSyntax alternative in variation.Alternatives)
                    Alternative(alternative, depth + 1);
                break;

            case OnceStatementSyntax once:
                Line(depth, $"@once{Tags(once.Tags)}");
                Statements(once.Body, depth + 1);
                break;
        }
    }

    /// <summary>Writes <c>- </c> and the first statement when it fits on one line, and a bare <c>-</c> with a block otherwise.</summary>
    private void Alternative(AlternativeSyntax alternative, int depth)
    {
        if (SingleLine(alternative.Body[0]) is { } first)
        {
            Line(depth, $"- {first}");
            Statements(alternative.Body.Skip(1), depth + 1);
        }
        else
        {
            Line(depth, "-");
            Statements(alternative.Body, depth + 1);
        }
    }

    /// <summary>The line a statement is written on, or <see langword="null"/> for a statement that opens a block.</summary>
    private static string? SingleLine(StatementSyntax statement) => statement switch
    {
        TextLineSyntax line => TextLine(line),
        SetStatementSyntax set => $"@set ${set.Variable.Name} {set.Operator switch { AssignmentOperator.Add => "+=", AssignmentOperator.Subtract => "-=", _ => "=" }} {Expression(set.Value)}",
        JumpStatementSyntax jump => $"@jump {jump.Target.Text}",
        CallStatementSyntax call => $"@call {call.Target.Text}{Tags(call.Tags)}",
        ReturnStatementSyntax => "@return",
        EndStatementSyntax => "@end",
        WaitStatementSyntax wait => $"@wait {Expression(wait.Duration)}",
        CommandStatementSyntax command => $"@{command.Command.Text}{Arguments(command.Arguments, command.Wait)}",
        _ => null,
    };

    private static string TextLine(TextLineSyntax line)
    {
        string speaker = line.Speaker is null ? "" : line.Pose is null ? $"{line.Speaker.Text}:" : $"{line.Speaker.Text} ({line.Pose.Text}):";
        string content = Inline(line.Content, option: false, narration: line.Speaker is null);
        if (line.Speaker is null && content is ['-' or '/' or '=' or '@', ..])
            content = "\\" + content;

        return string.Join(" ", new[] { speaker, content }.Where(part => part.Length > 0)) + Tags(line.Tags);
    }

    private static string Option(OptionSyntax option)
    {
        string text = Inline(option.Text, option: true, narration: false);
        string condition = option.Condition is null ? "" : $" @if {Expression(option.Condition)}";
        return $"->{(text.Length > 0 ? " " : "")}{text}{condition}{(option.IsOnce ? " @once" : "")}{Tags(option.Tags)}";
    }

    private static string Tags(IEnumerable<TagSyntax> tags) =>
        string.Concat(tags.Select(tag => tag.Value is null ? $" #{tag.Name}" : $" #{tag.Name}:{tag.Value}"));

    private static string Arguments(IEnumerable<ArgumentSyntax> arguments, CommandWait wait)
    {
        string written = string.Concat(arguments.Select(argument => argument.Name is null ? $" {Expression(argument.Value)}" : $" {argument.Name.Text}={Expression(argument.Value)}"));
        return wait switch
        {
            CommandWait.Wait => written + " wait",
            CommandWait.NoWait => written + " nowait",
            _ => written,
        };
    }

    private static string Inline(IEnumerable<InlineSyntax> items, bool option, bool narration) =>
        string.Concat(items.Select(item => Inline(item, option, narration)));

    private static string Inline(InlineSyntax item, bool option, bool narration) => item switch
    {
        TextRunSyntax run => Escape(run.Text, option, narration),
        MarkupSyntax markup => $"[{markup.Name.Text}{Arguments(markup.Arguments, CommandWait.Default)}]{Inline(markup.Content, option, narration)}[/{markup.Name.Text}]",
        InterpolationSyntax interpolation => $"{{{Expression(interpolation.Value)}}}",
        InlineCommandSyntax command => $"{{@{command.Command.Text}{Arguments(command.Arguments, command.Wait)}}}",
        PauseSyntax pause => pause.Duration is null ? "{w}" : $"{{w {Expression(pause.Duration)}}}",
        PageBreakSyntax => "{p}",
        LineBreakSyntax => "{br}",
        IconSyntax icon => $"{{icon {icon.Name.Text}}}",
        ConditionalTextSyntax conditional =>
            $"{{if {Expression(conditional.Condition)}}}{Inline(conditional.Content, option, narration)}"
            + string.Concat(conditional.ElseIfs.Select(elseIf => $"{{elif {Expression(elseIf.Condition)}}}{Inline(elseIf.Content, option, narration)}"))
            + (conditional.Else is { } @else ? $"{{else}}{Inline(@else.Content, option, narration)}" : "")
            + "{/if}",
        _ => throw new ArgumentException($"No printed form for {item.GetType().Name}.", nameof(item)),
    };

    private static string Escape(string text, bool option, bool narration)
    {
        var escaped = new StringBuilder();
        foreach (char c in text)
        {
            if (c is '\\' or '[' or '{' or '#' || option && c is '@' || narration && c is ':')
                escaped.Append('\\');

            escaped.Append(c);
        }

        return escaped.ToString();
    }

    private static string Expression(ExpressionSyntax expression) => expression switch
    {
        NumberLiteralSyntax number => Number(number.Value),
        DurationLiteralSyntax duration => Number(duration.Seconds) + "s",
        StringLiteralSyntax @string => $"\"{@string.Value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        BooleanLiteralSyntax boolean => boolean.Value ? "true" : "false",
        VariableExpressionSyntax variable => $"${variable.Name}",
        NameExpressionSyntax name => name.Name,
        CallExpressionSyntax call => $"{call.Function.Text}({string.Join(", ", call.Arguments.Select(Expression))})",
        ParenthesizedExpressionSyntax parenthesized => $"({Expression(parenthesized.Expression)})",
        UnaryExpressionSyntax { Operator: UnaryOperator.Not } unary => $"not {Expression(unary.Operand)}",
        UnaryExpressionSyntax unary => $"-{Expression(unary.Operand)}",
        BinaryExpressionSyntax binary => $"{Expression(binary.Left)} {Operator(binary.Operator)} {Expression(binary.Right)}",
        _ => throw new ArgumentException($"No printed form for {expression.GetType().Name}.", nameof(expression)),
    };

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

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
