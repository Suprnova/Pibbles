using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Walks a file's statements, text and expressions, for the passes that look at every one of them.</summary>
internal static class SyntaxWalk
{
    /// <summary>The blocks a statement holds: an <c>@if</c>'s branches, a choice's options, a variation's alternatives, or <c>@once</c>'s block.</summary>
    /// <param name="statement">The statement to look in.</param>
    /// <returns>Its blocks in the order they're written, or none if it holds none.</returns>
    public static IEnumerable<IReadOnlyList<StatementSyntax>> BlocksOf(StatementSyntax statement) => statement switch
    {
        IfStatementSyntax @if => Branches(@if),
        ChoiceSyntax choice => choice.Options.Select(option => option.Body),
        VariationStatementSyntax variation => variation.Alternatives.Select(alternative => alternative.Body),
        OnceStatementSyntax once => [once.Body],
        _ => [],
    };

    private static IEnumerable<IReadOnlyList<StatementSyntax>> Branches(IfStatementSyntax @if)
    {
        yield return @if.Body;
        foreach (ElseIfClauseSyntax elseIf in @if.ElseIfs)
            yield return elseIf.Body;

        if (@if.Else is { } @else)
            yield return @else.Body;
    }

    /// <summary>
    /// Whether a statement always leaves its block: <c>@jump</c>, <c>@end</c> or <c>@return</c>, or an <c>@if</c> with an
    /// <c>@else</c> whose every branch leaves. Conditions are never evaluated, and choices and variations never count.
    /// </summary>
    /// <param name="statement">The statement to check.</param>
    /// <returns><see langword="true"/> if it always leaves its block.</returns>
    public static bool AlwaysLeaves(StatementSyntax statement) => statement switch
    {
        JumpStatementSyntax or EndStatementSyntax or ReturnStatementSyntax => true,
        IfStatementSyntax { Else: { } @else } @if => AlwaysLeaves(@if.Body) && @if.ElseIfs.All(elseIf => AlwaysLeaves(elseIf.Body)) && AlwaysLeaves(@else.Body),
        _ => false,
    };

    /// <summary>Whether a block always leaves, because one of its statements does.</summary>
    /// <param name="block">The block to check.</param>
    /// <returns><see langword="true"/> if one of its statements always leaves.</returns>
    public static bool AlwaysLeaves(IReadOnlyList<StatementSyntax> block) => block.Any(AlwaysLeaves);

    /// <summary>Every statement in a block and in the blocks under it, in the order they're written.</summary>
    /// <param name="block">The block to walk.</param>
    /// <returns>The block's statements, each followed by the statements inside it.</returns>
    public static IEnumerable<StatementSyntax> Statements(IEnumerable<StatementSyntax> block)
    {
        foreach (StatementSyntax statement in block)
        {
            yield return statement;
            foreach (IReadOnlyList<StatementSyntax> inner in BlocksOf(statement))
            {
                foreach (StatementSyntax nested in Statements(inner))
                    yield return nested;
            }
        }
    }

    /// <summary>
    /// Whether a block holds, at any depth, a statement that counts how often it ran: <c>@sequence</c>, <c>@cycle</c>,
    /// <c>@once</c>, or a choice with an <c>@once</c> option. The count is kept for each block, so two blocks written the
    /// same don't behave like one.
    /// </summary>
    /// <param name="block">The block to look in.</param>
    /// <returns><see langword="true"/> if the block holds such a statement.</returns>
    public static bool HoldsState(IEnumerable<StatementSyntax> block) => Statements(block).Any(statement =>
        statement is VariationStatementSyntax or OnceStatementSyntax || statement is ChoiceSyntax choice && choice.Options.Any(option => option.IsOnce));

    /// <summary>The text on a statement's own line: a text line's content, or each option's text.</summary>
    /// <param name="statement">The statement to look in.</param>
    /// <returns>Its runs of text, or none if its line holds none.</returns>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> TextOf(StatementSyntax statement) => statement switch
    {
        TextLineSyntax line => [line.Content],
        ChoiceSyntax choice => choice.Options.Select(option => option.Text),
        _ => [],
    };

    /// <summary>A run of text, and every run inside its markup spans and conditional branches.</summary>
    /// <param name="content">The run to start from.</param>
    /// <returns>The run itself, then each run inside it, at any depth, in the order they're written.</returns>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> Runs(IReadOnlyList<InlineSyntax> content)
    {
        yield return content;
        foreach (InlineSyntax item in content)
        {
            foreach (IReadOnlyList<InlineSyntax> inner in InnerRuns(item))
            {
                foreach (IReadOnlyList<InlineSyntax> run in Runs(inner))
                    yield return run;
            }
        }
    }

    /// <summary>The runs of text an inline item holds: a span's content, or each branch of conditional text.</summary>
    /// <param name="item">The item to look in.</param>
    /// <returns>Its runs, or none if it holds none.</returns>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> InnerRuns(InlineSyntax item) => item switch
    {
        MarkupSyntax markup => [markup.Content],
        ConditionalTextSyntax conditional => Branches(conditional),
        _ => [],
    };

    /// <summary>Each branch of conditional text, in order, including the <c>{else}</c>.</summary>
    /// <param name="conditional">The conditional text.</param>
    /// <returns>The content of each branch.</returns>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> Branches(ConditionalTextSyntax conditional)
    {
        yield return conditional.Content;
        foreach (ElseIfTextSyntax elseIf in conditional.ElseIfs)
            yield return elseIf.Content;

        if (conditional.Else is { } @else)
            yield return @else.Content;
    }

    /// <summary>Every expression a statement's own line holds, in its text too, with every expression inside each one.</summary>
    /// <param name="statement">The statement to look in.</param>
    /// <returns>Each expression followed by the expressions inside it.</returns>
    public static IEnumerable<ExpressionSyntax> ExpressionsOf(StatementSyntax statement)
    {
        IEnumerable<ExpressionSyntax> own = statement switch
        {
            IfStatementSyntax @if => [@if.Condition, .. @if.ElseIfs.Select(elseIf => elseIf.Condition)],
            SetStatementSyntax set => [set.Variable, set.Value],
            PauseStatementSyntax pause => [pause.Duration],
            CommandStatementSyntax command => command.Arguments.Select(argument => argument.Value),
            ChoiceSyntax choice => choice.Options.Select(option => option.Condition).OfType<ExpressionSyntax>(),
            _ => [],
        };

        IEnumerable<ExpressionSyntax> inText = TextOf(statement).SelectMany(Runs).SelectMany(run => run.SelectMany(InlineExpressions));
        return own.Concat(inText).SelectMany(Subexpressions);
    }

    private static IEnumerable<ExpressionSyntax> InlineExpressions(InlineSyntax item) => item switch
    {
        MarkupSyntax markup => markup.Arguments.Select(argument => argument.Value),
        InterpolationSyntax interpolation => [interpolation.Value],
        InlineCommandSyntax command => command.Arguments.Select(argument => argument.Value),
        PauseSyntax pause => [pause.Duration],
        SpeedSyntax { Factor: { } factor } => [factor],
        ConditionalTextSyntax conditional => [conditional.Condition, .. conditional.ElseIfs.Select(elseIf => elseIf.Condition)],
        _ => [],
    };

    /// <summary>An expression and every expression inside it, at any depth.</summary>
    /// <param name="expression">The expression to walk.</param>
    /// <returns>The expression itself, then its operands and arguments, each followed by the expressions inside it.</returns>
    public static IEnumerable<ExpressionSyntax> Subexpressions(ExpressionSyntax expression)
    {
        yield return expression;
        IEnumerable<ExpressionSyntax> operands = expression switch
        {
            CallExpressionSyntax call => call.Arguments,
            ParenthesizedExpressionSyntax parenthesized => [parenthesized.Expression],
            UnaryExpressionSyntax unary => [unary.Operand],
            BinaryExpressionSyntax binary => [binary.Left, binary.Right],
            _ => [],
        };

        foreach (ExpressionSyntax operand in operands)
        {
            foreach (ExpressionSyntax inner in Subexpressions(operand))
                yield return inner;
        }
    }
}
