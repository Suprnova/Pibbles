using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Compiler;

/// <summary>The text of one line or option, lowered, for rendering.</summary>
/// <param name="Speaker">The speaker, or <see langword="null"/> for narration and for options.</param>
/// <param name="Tags">Every tag on the line, <c>#id</c> included, in the order written.</param>
/// <param name="Content">What the line shows and does, in order.</param>
internal sealed record Template(ActorSymbol? Speaker, IReadOnlyList<TemplateTag> Tags, IReadOnlyList<TemplateElement> Content);

/// <summary>A tag on a line or option.</summary>
/// <param name="Name">The tag's name, without the <c>#</c>.</param>
/// <param name="Value">The text after the <c>:</c> as written, or <see langword="null"/> if the tag has none.</param>
/// <param name="Member">The enum member the value names, for a tag declared with an enum type.</param>
internal sealed record TemplateTag(string Name, string? Value, EnumMemberSymbol? Member);

/// <summary>A piece of a line's content.</summary>
internal abstract record TemplateElement;

/// <summary>Plain text.</summary>
internal sealed record TextElement(string Text) : TemplateElement;

/// <summary>A markup span and what's inside it.</summary>
/// <param name="Markup">The markup.</param>
/// <param name="Arguments">The arguments, in the order of the markup's parameters, with defaults filled in.</param>
/// <param name="Children">The span's content.</param>
/// <param name="Location">Where the span is written, for a warning about its arguments.</param>
internal sealed record MarkupElement(MarkupSymbol Markup, IReadOnlyList<Expr> Arguments, IReadOnlyList<TemplateElement> Children, SourceLocation Location) : TemplateElement;

/// <summary>A value shown in the text. Its <see cref="Expr.Type"/> is <c>string</c>, <c>number</c> or <c>actor</c>, which are formatted differently.</summary>
internal sealed record InterpolationElement(Expr Value) : TemplateElement;

/// <summary>A command that runs when the reveal reaches this point.</summary>
/// <param name="Command">The command.</param>
/// <param name="Arguments">The arguments, in the order of the command's parameters, with defaults filled in.</param>
/// <param name="Waits">Whether the reveal waits for the command to finish.</param>
/// <param name="Location">Where the command is written, for a warning about its arguments.</param>
internal sealed record CommandElement(CommandSymbol Command, IReadOnlyList<Expr> Arguments, bool Waits, SourceLocation Location) : TemplateElement;

/// <summary><c>{w}</c>: waits for the player.</summary>
internal sealed record InputWaitElement : TemplateElement;

/// <summary><c>{w d}</c>: pauses the reveal.</summary>
/// <param name="Duration">How long.</param>
/// <param name="Location">Where the pause is written, for a warning.</param>
internal sealed record PauseElement(Expr Duration, SourceLocation Location) : TemplateElement;

/// <summary><c>{speed x}</c>: sets the reveal speed, relative to the player's setting, from here on.</summary>
/// <param name="Factor">The speed.</param>
/// <param name="Location">Where the speed is written, for a warning.</param>
internal sealed record SpeedElement(Expr Factor, SourceLocation Location) : TemplateElement;

/// <summary><c>{speed}</c>: returns to the player's speed setting.</summary>
internal sealed record SpeedResetElement : TemplateElement;

/// <summary><c>{p}</c>: waits for the player, clears the box and continues.</summary>
internal sealed record PageBreakElement : TemplateElement;

/// <summary><c>{br}</c>: a line break.</summary>
internal sealed record LineBreakElement : TemplateElement;

/// <summary><c>{icon name}</c>: an inline icon.</summary>
internal sealed record IconElement(IconSymbol Icon) : TemplateElement;

/// <summary>Conditional text: the content of the first branch whose condition is true, or else the <c>{else}</c> content.</summary>
/// <param name="Branches">The <c>{if}</c> branch, then each <c>{elif}</c>.</param>
/// <param name="Else">The <c>{else}</c> content, or <see langword="null"/>.</param>
internal sealed record ConditionalElement(IReadOnlyList<ConditionalBranch> Branches, IReadOnlyList<TemplateElement>? Else) : TemplateElement;

/// <summary>One <c>{if}</c> or <c>{elif}</c> branch of conditional text.</summary>
internal sealed record ConditionalBranch(Expr Condition, IReadOnlyList<TemplateElement> Content);
