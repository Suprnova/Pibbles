namespace Pibbles.Syntax;

/// <summary>A piece of inline text: what follows a text line's speaker, or an option's text.</summary>
/// <remarks><c>[square brackets]</c> are ranges (markup spans), and <c>{curly braces}</c> are points or computed values.</remarks>
public abstract record InlineSyntax : SyntaxNode;

/// <summary>A run of plain text.</summary>
/// <param name="Text">The text, with escapes resolved, so <c>\#</c> is <c>#</c>.</param>
public sealed record TextRunSyntax(string Text) : InlineSyntax;

/// <summary>A markup span, <c>[name args]…[/name]</c>, which styles or paces the text inside it.</summary>
/// <param name="Name">The markup's name.</param>
/// <param name="Arguments">The arguments in the opening bracket.</param>
/// <param name="Content">The text inside the span.</param>
public sealed record MarkupSyntax(NameSyntax Name, IReadOnlyList<ArgumentSyntax> Arguments, IReadOnlyList<InlineSyntax> Content) : InlineSyntax;

/// <summary>A computed value shown in the text: <c>{$var}</c> or <c>{fn(args)}</c>.</summary>
/// <param name="Value">A variable or a call.</param>
public sealed record InterpolationSyntax(ExpressionSyntax Value) : InlineSyntax;

/// <summary><c>{@command args}</c>: a command that runs at this point of the reveal.</summary>
/// <param name="Command">The command's name, without the <c>@</c>.</param>
/// <param name="Arguments">The arguments: positional ones first, then named ones.</param>
/// <param name="Wait">Whether the point overrides the command's declared waiting.</param>
public sealed record InlineCommandSyntax(NameSyntax Command, IReadOnlyList<ArgumentSyntax> Arguments, CommandWait Wait) : InlineSyntax;

/// <summary><c>{w}</c>, which waits for the player, or <c>{w 0.5}</c>, which pauses the reveal.</summary>
/// <param name="Duration">How long to pause, or <see langword="null"/> to wait for the player.</param>
public sealed record PauseSyntax(ExpressionSyntax? Duration) : InlineSyntax;

/// <summary><c>{speed 2}</c>, which sets the reveal speed from this point on, or <c>{speed}</c>, which returns to the player's setting.</summary>
/// <param name="Factor">The speed relative to the player's setting, or <see langword="null"/> to return to it.</param>
public sealed record SpeedSyntax(ExpressionSyntax? Factor) : InlineSyntax;

/// <summary><c>{p}</c>: waits for the player, clears the box, and continues.</summary>
public sealed record PageBreakSyntax : InlineSyntax;

/// <summary><c>{br}</c>: a line break.</summary>
public sealed record LineBreakSyntax : InlineSyntax;

/// <summary><c>{icon name}</c>: an inline icon, which counts as one character.</summary>
/// <param name="Name">The icon's name.</param>
public sealed record IconSyntax(NameSyntax Name) : InlineSyntax;

/// <summary>Conditional text: <c>{if cond}…{elif cond}…{else}…{/if}</c>.</summary>
/// <param name="Condition">The first branch's condition.</param>
/// <param name="Content">The text shown when the condition is true.</param>
/// <param name="ElseIfs">The <c>{elif}</c> branches, in order.</param>
/// <param name="Else">The <c>{else}</c> branch, or <see langword="null"/>.</param>
public sealed record ConditionalTextSyntax(ExpressionSyntax Condition, IReadOnlyList<InlineSyntax> Content, IReadOnlyList<ElseIfTextSyntax> ElseIfs, ElseTextSyntax? Else) : InlineSyntax;

/// <summary>An <c>{elif cond}</c> branch of conditional text.</summary>
/// <param name="Condition">The branch's condition.</param>
/// <param name="Content">The text shown when no earlier condition was true and this one is.</param>
public sealed record ElseIfTextSyntax(ExpressionSyntax Condition, IReadOnlyList<InlineSyntax> Content) : SyntaxNode;

/// <summary>The <c>{else}</c> branch of conditional text.</summary>
/// <param name="Content">The text shown when no condition was true.</param>
public sealed record ElseTextSyntax(IReadOnlyList<InlineSyntax> Content) : SyntaxNode;
