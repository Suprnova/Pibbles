namespace Pibbles.Syntax;

/// <summary>A statement in a node's body or in a block.</summary>
public abstract record StatementSyntax : SyntaxNode;

/// <summary><c>@jump node</c>: continues at another node, leaving the call stack as it is.</summary>
/// <param name="Target">The node to continue at.</param>
public sealed record JumpStatementSyntax(NameSyntax Target) : StatementSyntax;

/// <summary><c>@call node</c>: runs another node, then comes back.</summary>
/// <param name="Target">The node to run.</param>
/// <param name="Tags">The tags after the target, such as its <c>#id</c>.</param>
public sealed record CallStatementSyntax(NameSyntax Target, IReadOnlyList<TagSyntax> Tags) : StatementSyntax;

/// <summary><c>@return</c>: returns from the current <c>@call</c>, or ends the dialogue at the top level.</summary>
public sealed record ReturnStatementSyntax : StatementSyntax;

/// <summary><c>@end</c>: ends the dialogue and clears the call stack.</summary>
public sealed record EndStatementSyntax : StatementSyntax;

/// <summary><c>@sequence:</c> or <c>@cycle:</c>, and the alternatives in its block.</summary>
/// <param name="Kind">Which alternative runs on each entry.</param>
/// <param name="Tags">The tags after the <c>:</c>, which hold the block's <c>#id</c>.</param>
/// <param name="Alternatives">The alternatives, in order.</param>
public sealed record VariationStatementSyntax(VariationKind Kind, IReadOnlyList<TagSyntax> Tags, IReadOnlyList<AlternativeSyntax> Alternatives) : StatementSyntax;

/// <summary>
/// One alternative of a variation: <c>- </c> and a single-line statement, with any indented lines under it; or a bare
/// <c>-</c> with the whole alternative in the block under it.
/// </summary>
/// <param name="Body">The alternative's statements.</param>
public sealed record AlternativeSyntax(IReadOnlyList<StatementSyntax> Body) : SyntaxNode;

/// <summary><c>@once:</c> and its block, which runs the first time execution reaches it and is skipped after that.</summary>
/// <param name="Tags">The tags after the <c>:</c>, which hold the block's <c>#id</c>.</param>
/// <param name="Body">The statements that run on the first entry.</param>
public sealed record OnceStatementSyntax(IReadOnlyList<TagSyntax> Tags, IReadOnlyList<StatementSyntax> Body) : StatementSyntax;

/// <summary><c>@if condition:</c> and its block, with any <c>@elif</c> and <c>@else</c> clauses after it.</summary>
/// <param name="Condition">The condition.</param>
/// <param name="Body">The statements that run when the condition is true.</param>
/// <param name="ElseIfs">The <c>@elif</c> clauses, in order.</param>
/// <param name="Else">The <c>@else</c> clause, or <see langword="null"/>.</param>
public sealed record IfStatementSyntax(ExpressionSyntax Condition, IReadOnlyList<StatementSyntax> Body, IReadOnlyList<ElseIfClauseSyntax> ElseIfs, ElseClauseSyntax? Else) : StatementSyntax;

/// <summary>An <c>@elif condition:</c> clause and its block.</summary>
/// <param name="Condition">The condition.</param>
/// <param name="Body">The statements that run when no earlier condition was true and this one is.</param>
public sealed record ElseIfClauseSyntax(ExpressionSyntax Condition, IReadOnlyList<StatementSyntax> Body) : SyntaxNode;

/// <summary>An <c>@else:</c> clause and its block.</summary>
/// <param name="Body">The statements that run when no condition was true.</param>
public sealed record ElseClauseSyntax(IReadOnlyList<StatementSyntax> Body) : SyntaxNode;

/// <summary><c>@set $variable = value</c>, or <c>+=</c> or <c>-=</c>.</summary>
/// <param name="Variable">The variable that changes.</param>
/// <param name="Operator">How it changes.</param>
/// <param name="OperatorSpan">Where the operator is written.</param>
/// <param name="Value">The value assigned, added or subtracted.</param>
public sealed record SetStatementSyntax(VariableExpressionSyntax Variable, AssignmentOperator Operator, TextSpan OperatorSpan, ExpressionSyntax Value) : StatementSyntax;

/// <summary><c>@wait duration</c>: pauses the story without showing text.</summary>
/// <param name="Duration">How long to wait.</param>
public sealed record WaitStatementSyntax(ExpressionSyntax Duration) : StatementSyntax;

/// <summary>A command on its own line, such as <c>@show mira left</c> or <c>@move mira offscreen nowait</c>.</summary>
/// <param name="Command">The command's name, without the <c>@</c>.</param>
/// <param name="Arguments">The arguments: positional ones first, then named ones.</param>
/// <param name="Wait">Whether the line overrides the command's declared waiting.</param>
public sealed record CommandStatementSyntax(NameSyntax Command, IReadOnlyList<ArgumentSyntax> Arguments, CommandWait Wait) : StatementSyntax;

/// <summary>An argument to a command or markup: a value, with a name when it's written <c>name=value</c>.</summary>
/// <param name="Name">The parameter's name, or <see langword="null"/> for a positional argument.</param>
/// <param name="Value">The value.</param>
public sealed record ArgumentSyntax(NameSyntax? Name, ExpressionSyntax Value) : SyntaxNode;

/// <summary>How <c>@set</c> changes its variable.</summary>
public enum AssignmentOperator
{
    /// <summary><c>=</c>: replaces the value.</summary>
    Assign,

    /// <summary><c>+=</c>: adds to the value.</summary>
    Add,

    /// <summary><c>-=</c>: subtracts from the value.</summary>
    Subtract,
}

/// <summary>Whether a command line overrides the command's declared waiting.</summary>
public enum CommandWait
{
    /// <summary>No override: the command waits if it's declared <c>waits</c>.</summary>
    Default,

    /// <summary><c>wait</c>: the story waits for the command to finish.</summary>
    Wait,

    /// <summary><c>nowait</c>: the story carries on at once.</summary>
    NoWait,
}

/// <summary>A line of dialogue or narration, such as <c>mira (smug): I knew you'd come.</c></summary>
/// <remarks>A line with a pose and no text, <c>mira (sad):</c>, changes the pose without showing a line.</remarks>
/// <param name="Speaker">The speaker, or <see langword="null"/> for narration.</param>
/// <param name="Pose">The pose in parentheses after the speaker, or <see langword="null"/>.</param>
/// <param name="Content">The line's text, with its leading and trailing whitespace trimmed.</param>
/// <param name="Tags">The tags at the end of the line.</param>
public sealed record TextLineSyntax(NameSyntax? Speaker, NameSyntax? Pose, IReadOnlyList<InlineSyntax> Content, IReadOnlyList<TagSyntax> Tags) : StatementSyntax;

/// <summary>A choice: consecutive <c>-></c> options at the same indentation.</summary>
/// <param name="Options">The options, in order.</param>
public sealed record ChoiceSyntax(IReadOnlyList<OptionSyntax> Options) : StatementSyntax;

/// <summary>One option of a choice, such as <c>-> Use the key  @if $has_key #show_disabled</c>, and its block.</summary>
/// <param name="Text">The option's text.</param>
/// <param name="Condition">The <c>@if</c> modifier's condition, or <see langword="null"/>.</param>
/// <param name="IsOnce">Whether the option has the <c>@once</c> modifier, which removes it after it's picked.</param>
/// <param name="Tags">The tags at the end of the line.</param>
/// <param name="Body">The statements that run when the option is picked.</param>
public sealed record OptionSyntax(IReadOnlyList<InlineSyntax> Text, ExpressionSyntax? Condition, bool IsOnce, IReadOnlyList<TagSyntax> Tags, IReadOnlyList<StatementSyntax> Body) : SyntaxNode;

/// <summary>Which alternative a variation runs on each entry.</summary>
public enum VariationKind
{
    /// <summary><c>@sequence:</c> runs the next alternative each time, then stays on the last.</summary>
    Sequence,

    /// <summary><c>@cycle:</c> runs the next alternative each time, starting over after the last.</summary>
    Cycle,
}
