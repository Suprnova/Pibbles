namespace Pibbles.Diagnostics;

public static partial class DiagnosticCatalog
{
    /// <summary>PIB2001: a second <c>name:</c> line in an <c>@actor</c> or <c>@narration</c> block.</summary>
    public static DiagnosticDescriptor RepeatedDisplayName { get; } = new(
        "PIB2001",
        DiagnosticSeverity.Error,
        "This block already has a `name:`.",
        help: "An actor has one display name. Keep one of the two lines.");

    /// <summary>PIB2002: the character U+FFFC in text, which Pibbles uses to stand for an icon.</summary>
    public static DiagnosticDescriptor ObjectReplacementCharacter { get; } = new(
        "PIB2002",
        DiagnosticSeverity.Error,
        "This text has U+FFFC in it, which I use internally to mark where an icon goes.",
        help: "Remove it. To show an icon, write `{{icon name}}`.");

    /// <summary>PIB2003: a speaker with neither values nor text.</summary>
    /// <remarks>Arguments: the speaker's name as written, and a <see cref="SpeakerMention"/> of it for the help's prose.</remarks>
    public static DiagnosticDescriptor EmptyLine { get; } = new(
        "PIB2003",
        DiagnosticSeverity.Error,
        "`{0}:` has nothing after it.",
        help: "Write what {1} says after the colon, or write `{0}: {{w}}` for a box with only their name. To set their values without a line, write `{0} (value):`.");

    /// <summary>PIB2004: a speaker that isn't a declared actor.</summary>
    /// <remarks>
    /// Arguments: the speaker as written; <see cref="DidYouMean"/>'s sentence for the closest actor, or nothing; and the line's
    /// start through its colon, with the colon escaped.
    /// </remarks>
    public static DiagnosticDescriptor UnknownSpeaker { get; } = new(
        "PIB2004",
        DiagnosticSeverity.Error,
        "I don't know an actor called `{0}`.",
        help: "{1}If this line is narration, escape the colon: `{2}`.");

    /// <summary>PIB2005: a value the speaker doesn't have.</summary>
    /// <remarks>Arguments: the actor's display name; the value; and a suggestion, the actor's values or where the value went, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownChannelValue { get; } = new(
        "PIB2005",
        DiagnosticSeverity.Error,
        "{0} has no value called `{1}`.",
        help: "{2}");

    /// <summary>PIB2006: a parenthesis after a name, before a colon, that doesn't hold a list of names, as in <c>mira (to Rex):</c>.</summary>
    /// <remarks>Arguments: the parenthesis, and the line's start through its colon, with the colon escaped.</remarks>
    public static DiagnosticDescriptor NotValues { get; } = new(
        "PIB2006",
        DiagnosticSeverity.Error,
        "`{0}` isn't a list of values: each value is a single name.",
        help: "For how a line is said, use a `//` comment. If this is narration, escape the colon: `{1}`.");

    /// <summary>PIB2007: a declared actor and a colon with no space after it, as in <c>mira:Hi</c>.</summary>
    /// <remarks>Arguments: the line's start up to its colon, and the line with its colon escaped.</remarks>
    public static DiagnosticDescriptor NoSpaceAfterSpeaker { get; } = new(
        "PIB2007",
        DiagnosticSeverity.Warning,
        "There's no space after `{0}:`.",
        help: "Add one, or escape the colon if this is narration: `{1}`.");

    /// <summary>PIB2008: more between a declared actor's values and the colon, as in <c>mira (smug) (shake):</c> or <c>mira (smug) shake:</c>.</summary>
    /// <remarks>Arguments: what comes after the values; the values' parenthesis; and the line's start through its colon, with the colon escaped.</remarks>
    public static DiagnosticDescriptor AfterValues { get; } = new(
        "PIB2008",
        DiagnosticSeverity.Error,
        "`{0}` has no place after `{1}`, before the colon.",
        help: "A speaker takes its values in one parenthesis and nothing else before the colon. For how a line is said, use a `//` comment. If this is narration, escape the colon: `{2}`.");

    /// <summary>PIB2009: two values of one channel on one line, as in <c>mira (mad, sad):</c>.</summary>
    /// <remarks>Arguments: the first value; the second value; the speaker's display name; and the channel.</remarks>
    public static DiagnosticDescriptor TwoValuesInChannel { get; } = new(
        "PIB2009",
        DiagnosticSeverity.Error,
        "`{0}` and `{1}` are both values of {2}'s `{3}`, and a line sets one value of a channel.",
        help: "Keep the one you mean.");

    /// <summary>PIB2010: a value that two of a speaker's channels share.</summary>
    /// <remarks>Arguments: the value; where it already is, such as <c>`effect`</c> or <c>Mira's `effect`</c>; and the channel that can't have it too.</remarks>
    public static DiagnosticDescriptor ValueInTwoChannels { get; } = new(
        "PIB2010",
        DiagnosticSeverity.Error,
        "`{0}` is already a value of {1}, so `{2}` can't have it too.",
        help: "A line names a value without its channel, so each value belongs to one channel. Give one of them another name.");

    /// <summary>PIB2011: a channel an actor's block names that isn't declared.</summary>
    /// <remarks>Arguments: the channel as written, and the closest declared channel, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownChannel { get; } = new(
        "PIB2011",
        DiagnosticSeverity.Error,
        "I don't know a channel called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2012: a second <c>@narration</c> in the story.</summary>
    /// <remarks>Argument: where the first one is, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor SecondNarration { get; } = new(
        "PIB2012",
        DiagnosticSeverity.Error,
        "There's already an `@narration`, {0}.",
        help: "Narration has one block. Merge the two, or remove one.");

    /// <summary>PIB2101: a command, <c>{w}</c>, <c>{page}</c> or <c>{speed}</c> in an option's text.</summary>
    /// <remarks>Argument: the point, such as <c>{w}</c> or <c>{@jolt}</c>.</remarks>
    public static DiagnosticDescriptor NotInOption { get; } = new(
        "PIB2101",
        DiagnosticSeverity.Error,
        "An option's text can't contain `{0}`.",
        help: "Pauses, speed changes and commands go in the indented lines under the option.");

    /// <summary>PIB2102: an option with no text.</summary>
    public static DiagnosticDescriptor EmptyOption { get; } = new(
        "PIB2102",
        DiagnosticSeverity.Info,
        "This option has no text.",
        help: "Write what the player picks after the `->`.");

    /// <summary>PIB2201: an <c>@elif</c> or <c>@else</c> with no <c>@if</c> directly above it.</summary>
    /// <remarks>Argument: the keyword, <c>@elif</c> or <c>@else</c>.</remarks>
    public static DiagnosticDescriptor StrayClause { get; } = new(
        "PIB2201",
        DiagnosticSeverity.Error,
        "This `{0}` has no `@if` to belong to.",
        help: "Put it right after the `@if` block, at the same indentation as the `@if`.");

    /// <summary>PIB2202: a block opener with a <c>:</c> after it.</summary>
    /// <remarks>Arguments: the keyword, and the line up to the <c>:</c>.</remarks>
    public static DiagnosticDescriptor OpenerColon { get; } = new(
        "PIB2202",
        DiagnosticSeverity.Error,
        "I didn't expect a `:` on this `{0}` line.",
        help: "Remove it: `{1}`.");

    /// <summary>PIB2203: a condition that isn't a <c>bool</c>.</summary>
    /// <remarks>Arguments: the condition as written; its type with its article; and a condition that's probably meant, such as <c>$attempts &gt; 0</c>, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NotCondition { get; } = new(
        "PIB2203",
        DiagnosticSeverity.Error,
        "`{0}` is {1}, but a condition has to be true or false.",
        help: "Did you mean `{2}`?");

    /// <summary>PIB2301: a name where a variable is expected, written without its <c>$</c>.</summary>
    /// <remarks>Arguments: the name as written, and the line with a <c>$</c> before the name.</remarks>
    public static DiagnosticDescriptor MissingDollar { get; } = new(
        "PIB2301",
        DiagnosticSeverity.Error,
        "Variables start with `$`, so I need one before `{0}`.",
        help: "Write `{1}`.");

    /// <summary>PIB2302: a variable that isn't declared.</summary>
    /// <remarks>Arguments: the variable, with its <c>$</c>, and the closest variable, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownVariable { get; } = new(
        "PIB2302",
        DiagnosticSeverity.Error,
        "I don't know a variable called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2303: a value whose type isn't the type where it's used: a variable's value, a default, or <c>@pause</c>'s time.</summary>
    /// <remarks>
    /// Arguments: what the value is for, such as <c>`$has_key` holds a `bool`</c>; the value's type with its article; and
    /// what values to use instead, or <see langword="null"/> to leave out the help.
    /// </remarks>
    public static DiagnosticDescriptor ValueType { get; } = new(
        "PIB2303",
        DiagnosticSeverity.Error,
        "{0}, but this is {1}.",
        help: "{2}");

    /// <summary>PIB2304: a variable whose initial value is a name, with no type written.</summary>
    /// <remarks>
    /// Arguments: the variable, with its <c>$</c>; the initial value; and the declaration with its type written out. The
    /// help is left out when the value names no enum member, actor or node, or more than one kind of them.
    /// </remarks>
    public static DiagnosticDescriptor UntypedVariable { get; } = new(
        "PIB2304",
        DiagnosticSeverity.Error,
        "`{0}` starts as `{1}`, so I need its type written out.",
        help: "Write the type after the variable: `{2}`.");

    /// <summary>PIB2401: a statement after one that always leaves its block.</summary>
    /// <remarks>Arguments: why it never runs, such as <c>of the `@jump` above it</c>, and the statement that leaves, such as <c>@jump</c>.</remarks>
    public static DiagnosticDescriptor NeverRuns { get; } = new(
        "PIB2401",
        DiagnosticSeverity.Warning,
        "This line never runs, because {0}.",
        help: "Remove it, or move it above the `{1}`.");

    /// <summary>PIB2402: an <c>@if</c> or <c>@elif</c> condition that is always false.</summary>
    public static DiagnosticDescriptor ConditionNeverTrue { get; } = new(
        "PIB2402",
        DiagnosticSeverity.Warning,
        "This condition is never true, so the lines under it never run.",
        help: "Remove them, or write a condition that can change.");

    /// <summary>PIB2403: an <c>@if</c> or <c>@elif</c> condition that is always true, with branches after it.</summary>
    public static DiagnosticDescriptor ConditionAlwaysTrue { get; } = new(
        "PIB2403",
        DiagnosticSeverity.Warning,
        "This condition is always true, so the branches after it never run.",
        help: "Remove the branches after it, or write a condition that can change.");

    /// <summary>PIB2501: a line in a variation block that isn't an alternative.</summary>
    /// <remarks>Argument: the variation's keyword, such as <c>@cycle</c>.</remarks>
    public static DiagnosticDescriptor NotAlternative { get; } = new(
        "PIB2501",
        DiagnosticSeverity.Error,
        "A `{0}` block can only hold alternatives.",
        help: "Start each alternative with `- `.");

    /// <summary>PIB2502: a block opener right after an alternative's <c>- </c>.</summary>
    /// <remarks>Argument: the opener, such as <c>@if</c> or <c>-></c>.</remarks>
    public static DiagnosticDescriptor OpenerInAlternative { get; } = new(
        "PIB2502",
        DiagnosticSeverity.Error,
        "`{0}` can't start an alternative.",
        help: "Put `-` on its own line, and the `{0}` on the line below it, indented.");

    /// <summary>PIB2601: a positional argument after a named one.</summary>
    public static DiagnosticDescriptor ArgumentOrder { get; } = new(
        "PIB2601",
        DiagnosticSeverity.Error,
        "This argument has no name, but it comes after one that does.",
        help: "Put unnamed arguments first, then named ones, then `wait` or `nowait`.");

    /// <summary>PIB2602: a space between a function's name and its <c>(</c>, where only a call can follow the name.</summary>
    /// <remarks>Argument: the function's name.</remarks>
    public static DiagnosticDescriptor SpaceBeforeCall { get; } = new(
        "PIB2602",
        DiagnosticSeverity.Error,
        "There's a space between `{0}` and its `(`.",
        help: "To call `{0}`, remove the space: `{0}(…)`.");

    /// <summary>PIB2603: a negative number as a command argument, without parentheses.</summary>
    /// <remarks>Argument: the negative value as written, such as <c>-1</c>.</remarks>
    public static DiagnosticDescriptor NegativeArgument { get; } = new(
        "PIB2603",
        DiagnosticSeverity.Error,
        "A negative argument has to go in brackets.",
        help: "Write `({0})`.");

    /// <summary>PIB2604: a command that isn't declared.</summary>
    /// <remarks>Arguments: the command's name, without its <c>@</c>, and the closest command, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownCommand { get; } = new(
        "PIB2604",
        DiagnosticSeverity.Error,
        "I don't know a command called `@{0}`.",
        help: "Did you mean `@{1}`?");

    /// <summary>PIB2605: an argument whose type isn't the type of its parameter.</summary>
    /// <remarks>
    /// Arguments: what takes the argument, such as <c>@show</c> or <c>has_item()</c>; the parameter's type with its
    /// article; the parameter; the argument's type with its article; and what values to use instead, or
    /// <see langword="null"/> to leave out the help.
    /// </remarks>
    public static DiagnosticDescriptor ArgumentType { get; } = new(
        "PIB2605",
        DiagnosticSeverity.Error,
        "`{0}` expects {1} for `{2}`, but this is {3}.",
        help: "{4}");

    /// <summary>PIB2606: a command inside a line that isn't declared <c>inline</c>.</summary>
    /// <remarks>Argument: the command's name, without its <c>@</c>.</remarks>
    public static DiagnosticDescriptor NotInline { get; } = new(
        "PIB2606",
        DiagnosticSeverity.Error,
        "`@{0}` can't be used inside a line, because it isn't declared `inline`.",
        help: "Put it on its own `@` line.");

    /// <summary>PIB2607: more arguments than parameters.</summary>
    /// <remarks>Arguments: what takes the arguments, such as <c>has_item()</c>, and how many it takes, such as <c>only 1 argument</c>.</remarks>
    public static DiagnosticDescriptor TooManyArguments { get; } = new(
        "PIB2607",
        DiagnosticSeverity.Error,
        "`{0}` takes {1}.",
        help: "Remove the extra arguments.");

    /// <summary>PIB2608: a named argument for a parameter that doesn't exist.</summary>
    /// <remarks>Arguments: what takes the arguments, such as <c>@show</c>; the name; and the closest parameter, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownParameter { get; } = new(
        "PIB2608",
        DiagnosticSeverity.Error,
        "`{0}` has no parameter called `{1}`.",
        help: "Did you mean `{2}`?");

    /// <summary>PIB2609: a parameter with no default and no argument.</summary>
    /// <remarks>Arguments: what takes the arguments; the missing parameters, such as <c>a value for `to`</c>; and how to add them.</remarks>
    public static DiagnosticDescriptor MissingArgument { get; } = new(
        "PIB2609",
        DiagnosticSeverity.Error,
        "`{0}` needs {1}.",
        help: "{2}");

    /// <summary>PIB2610: a named argument for a parameter that already has one.</summary>
    /// <remarks>Arguments: what takes the arguments, and the parameter.</remarks>
    public static DiagnosticDescriptor RepeatedArgument { get; } = new(
        "PIB2610",
        DiagnosticSeverity.Error,
        "`{0}` already has a value for `{1}`.",
        help: "Remove one of them.");

    /// <summary>PIB2611: a call to a function that isn't declared.</summary>
    /// <remarks>Arguments: the function's name, and the closest function, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownFunction { get; } = new(
        "PIB2611",
        DiagnosticSeverity.Error,
        "I don't know a function called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2612: a parameter with no default after one that has a default.</summary>
    /// <remarks>Argument: the parameter's name.</remarks>
    public static DiagnosticDescriptor RequiredAfterOptional { get; } = new(
        "PIB2612",
        DiagnosticSeverity.Error,
        "`{0}` has no default, but it comes after a parameter that has one.",
        help: "Put the parameters that have defaults last.");
}
