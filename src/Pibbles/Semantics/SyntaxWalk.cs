using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Walks a file's statements, text and expressions, for the passes that look at every one of them.</summary>
internal static class SyntaxWalk
{
    /// <summary>The blocks a statement holds: an <c>@if</c>'s branches, a choice's options, a variation's alternatives, or <c>@once</c>'s block.</summary>
    public static IEnumerable<IReadOnlyList<StatementSyntax>> BlocksOf(StatementSyntax statement) => statement switch
    {
        IfStatementSyntax @if => [@if.Body, .. @if.ElseIfs.Select(elseIf => elseIf.Body), .. @if.Else is { } @else ? [@else.Body] : Array.Empty<IReadOnlyList<StatementSyntax>>()],
        ChoiceSyntax choice => choice.Options.Select(option => option.Body),
        VariationStatementSyntax variation => variation.Alternatives.Select(alternative => alternative.Body),
        OnceStatementSyntax once => [once.Body],
        _ => [],
    };

    /// <summary>Every statement in a block and in the blocks under it, in the order they're written.</summary>
    public static IEnumerable<StatementSyntax> Statements(IEnumerable<StatementSyntax> block) =>
        block.SelectMany(statement => (IEnumerable<StatementSyntax>)[statement, .. BlocksOf(statement).SelectMany(Statements)]);

    /// <summary>The text on a statement's own line: a text line's content, or each option's text.</summary>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> TextOf(StatementSyntax statement) => statement switch
    {
        TextLineSyntax line => [line.Content],
        ChoiceSyntax choice => choice.Options.Select(option => option.Text),
        _ => [],
    };

    /// <summary>A run of text, and every run inside its markup spans and conditional branches.</summary>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> Runs(IReadOnlyList<InlineSyntax> content) =>
        [content, .. content.SelectMany(item => InnerRuns(item).SelectMany(Runs))];

    /// <summary>The runs of text an inline item holds: a span's content, or each branch of conditional text.</summary>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> InnerRuns(InlineSyntax item) => item switch
    {
        MarkupSyntax markup => [markup.Content],
        ConditionalTextSyntax conditional => Branches(conditional),
        _ => [],
    };

    /// <summary>Each branch of conditional text, in order, including the <c>{else}</c>.</summary>
    public static IEnumerable<IReadOnlyList<InlineSyntax>> Branches(ConditionalTextSyntax conditional) =>
        [conditional.Content, .. conditional.ElseIfs.Select(elseIf => elseIf.Content), .. conditional.Else is { } @else ? [@else.Content] : Array.Empty<IReadOnlyList<InlineSyntax>>()];

    /// <summary>Every expression a statement's own line holds, in its text too, with every expression inside each one.</summary>
    public static IEnumerable<ExpressionSyntax> ExpressionsOf(StatementSyntax statement)
    {
        IEnumerable<ExpressionSyntax> own = statement switch
        {
            IfStatementSyntax @if => [@if.Condition, .. @if.ElseIfs.Select(elseIf => elseIf.Condition)],
            SetStatementSyntax set => [set.Variable, set.Value],
            WaitStatementSyntax wait => [wait.Duration],
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
        PauseSyntax { Duration: { } duration } => [duration],
        SpeedSyntax { Factor: { } factor } => [factor],
        ConditionalTextSyntax conditional => [conditional.Condition, .. conditional.ElseIfs.Select(elseIf => elseIf.Condition)],
        _ => [],
    };

    public static IEnumerable<ExpressionSyntax> Subexpressions(ExpressionSyntax expression) =>
    [
        expression,
        .. (expression switch
        {
            CallExpressionSyntax call => call.Arguments,
            ParenthesizedExpressionSyntax parenthesized => [parenthesized.Expression],
            UnaryExpressionSyntax unary => [unary.Operand],
            BinaryExpressionSyntax binary => [binary.Left, binary.Right],
            _ => Array.Empty<ExpressionSyntax>(),
        }).SelectMany(Subexpressions),
    ];
}
