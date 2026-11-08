namespace Pibbles.Diagnostics;

public static partial class DiagnosticCatalog
{
    /// <summary>PIB3001: a <c>(</c> with no <c>)</c> on its line.</summary>
    public static DiagnosticDescriptor UnclosedParenthesis { get; } = new(
        "PIB3001",
        DiagnosticSeverity.Error,
        "I can't find the `)` that closes this `(`.",
        help: "Add the `)` on the same line.");

    /// <summary>PIB3002: two comparisons in a row, such as <c>$a &lt; $b &lt; $c</c>.</summary>
    /// <remarks>Argument: the whole chain as written.</remarks>
    public static DiagnosticDescriptor ChainedComparison { get; } = new(
        "PIB3002",
        DiagnosticSeverity.Error,
        "I can't compare three things at once: `{0}`.",
        help: "Compare two at a time, joined with `and`: `$a < $b and $b < $c`.");

    /// <summary>PIB3003: an operator used on types it doesn't work on.</summary>
    /// <remarks>Arguments: the operator; the operands' types, such as <c>a `string` and a `number`</c>; and what the operator works on.</remarks>
    public static DiagnosticDescriptor OperatorTypes { get; } = new(
        "PIB3003",
        DiagnosticSeverity.Error,
        "I can't use `{0}` with {1}.",
        help: "{2}");

    /// <summary>PIB3004: a bare name where no name can go, such as a whole condition or an operand of <c>+</c>.</summary>
    /// <remarks>Arguments: the name, and what was probably meant, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NameNotAllowed { get; } = new(
        "PIB3004",
        DiagnosticSeverity.Error,
        "I don't know what `{0}` means here.",
        help: "{1}");

    /// <summary>PIB3005: a bare name that isn't a value of the type expected where it appears.</summary>
    /// <remarks>Arguments: the name; the type with its article; and a suggestion, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NotValueOfType { get; } = new(
        "PIB3005",
        DiagnosticSeverity.Error,
        "`{0}` isn't {1}.",
        help: "{2}");

    /// <summary>PIB3006: <c>==</c> or <c>!=</c> between two bare names, which have no type to be read against.</summary>
    /// <remarks>Argument: the comparison as written.</remarks>
    public static DiagnosticDescriptor TwoNames { get; } = new(
        "PIB3006",
        DiagnosticSeverity.Error,
        "I can't compare two names: `{0}`.",
        help: "Compare a variable with a name instead, like `$where == left`.");

    /// <summary>PIB3007: a <c>/</c> or <c>%</c> whose right side is a constant zero.</summary>
    /// <remarks>Argument: the operation as written, such as <c>$x / 0</c>.</remarks>
    public static DiagnosticDescriptor DivisionByZero { get; } = new(
        "PIB3007",
        DiagnosticSeverity.Warning,
        "`{0}` divides by zero, so it gives 0.",
        help: "Divide by something that isn't zero.");

    /// <summary>What PIB3101 and PIB3301's help adds when the line has a trailing comment, which runs to the end of the line and so takes any closer written after it.</summary>
    public const string CommentSwallowedCloserHelp = " The `//` on this line starts a comment that runs to the end of it, so put the closer before the `//`.";

    /// <summary>PIB3101: a markup span with no close on its line, or none before a conditional-text boundary.</summary>
    /// <remarks>Arguments: the markup's name, and <see cref="CommentSwallowedCloserHelp"/> if the line has a trailing comment, or an empty string.</remarks>
    public static DiagnosticDescriptor UnclosedMarkup { get; } = new(
        "PIB3101",
        DiagnosticSeverity.Error,
        "I can't find the end of this `[{0}]`.",
        help: "Close it on the same line with `[/{0}]`.{1}");

    /// <summary>PIB3102: a markup close for an outer span while an inner one is still open.</summary>
    /// <remarks>Argument: the name of the innermost open span.</remarks>
    public static DiagnosticDescriptor MarkupOutOfOrder { get; } = new(
        "PIB3102",
        DiagnosticSeverity.Error,
        "I expected `[/{0}]` here, because `[{0}]` was opened last.",
        help: "Close markup in the reverse order you opened it: `[b][i]…[/i][/b]`.");

    /// <summary>PIB3103: a markup close with no matching open span.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor UnopenedMarkup { get; } = new(
        "PIB3103",
        DiagnosticSeverity.Error,
        "`[/{0}]` closes markup that was never opened.",
        help: "If you meant the text `[/{0}]`, put a backslash before it: `\\[/{0}]`.");

    /// <summary>PIB3104: markup that isn't declared or built in.</summary>
    /// <remarks>Arguments: the markup's name, and what it was probably meant to be, such as <c>clue</c> or <c>{speed}</c>, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownMarkup { get; } = new(
        "PIB3104",
        DiagnosticSeverity.Error,
        "I don't know markup called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>The help for PIB3201 when the point isn't an old spelling of one.</summary>
    public const string UnknownPointHelp = "To show a variable, write `{$name}`. To call a function, write `{name()}`. If the braces are part of the text, put a backslash before the `{`.";

    /// <summary>PIB3201: a point that isn't a variable, call, command or brace keyword, or is the old spelling <c>{p}</c> of <c>{page}</c>.</summary>
    /// <remarks>Arguments: the point as written, and how to fix it, which is <see cref="UnknownPointHelp"/> unless the point has a known replacement.</remarks>
    public static DiagnosticDescriptor UnknownPoint { get; } = new(
        "PIB3201",
        DiagnosticSeverity.Error,
        "I don't know what `{0}` means.",
        help: "{1}");

    /// <summary>PIB3202: a value after a point that takes none, such as <c>{w 0.5}</c>.</summary>
    /// <remarks>Arguments: the point's keyword, such as <c>w</c>; the value as written; and how to fix it.</remarks>
    public static DiagnosticDescriptor PointTakesNoValue { get; } = new(
        "PIB3202",
        DiagnosticSeverity.Error,
        "`{{{0}}}` takes no value, so I can't use `{1}` after it.",
        help: "{2}");

    /// <summary>PIB3203: an icon that isn't declared.</summary>
    /// <remarks>Arguments: the icon's name, and the closest icon, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownIcon { get; } = new(
        "PIB3203",
        DiagnosticSeverity.Error,
        "I don't know an icon called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB3204: a value shown in text whose type isn't text, a number or an actor.</summary>
    /// <remarks>Arguments: the value as written; its type with its article; and how to show text that depends on it.</remarks>
    public static DiagnosticDescriptor NotShowable { get; } = new(
        "PIB3204",
        DiagnosticSeverity.Error,
        "I can't show `{0}` in text, because it's {1}.",
        help: "{2}");

    /// <summary>PIB3205: a constant pacing value (a speed, a pause or a wait) that isn't more than zero.</summary>
    /// <remarks>Arguments: what the value is, such as <c>A pause</c>; the value as written; and how to write a valid one.</remarks>
    public static DiagnosticDescriptor NotPositive { get; } = new(
        "PIB3205",
        DiagnosticSeverity.Error,
        "{0} has to be more than zero, but `{1}` isn't.",
        help: "{2}");

    /// <summary>PIB3301: a bracket, brace or <c>{if}</c> that isn't closed on its line.</summary>
    /// <remarks>
    /// Arguments: the missing closer, such as <c>}</c> or <c>{/if}</c>; the opener it should close; and
    /// <see cref="CommentSwallowedCloserHelp"/> if the line has a trailing comment, or an empty string.
    /// </remarks>
    public static DiagnosticDescriptor Unclosed { get; } = new(
        "PIB3301",
        DiagnosticSeverity.Error,
        "I can't find the `{0}` that ends this `{1}`.",
        help: "Close it on the same line.{2}");
}
