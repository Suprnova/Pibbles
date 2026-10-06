namespace Pibbles.Diagnostics;

/// <summary>Every diagnostic code Pibbles reports.</summary>
public static class DiagnosticCatalog
{
    /// <summary>PIB1001: a line's indentation uses a different character than the rest of the file's.</summary>
    /// <remarks>Arguments: what this line indents with, and what the file indents with.</remarks>
    public static DiagnosticDescriptor MixedIndentation { get; } = new(
        "PIB1001",
        DiagnosticSeverity.Error,
        "This file indents with both tabs and spaces, so I can't tell which block this line belongs to.",
        "this line indents with {0}, but the file indents with {1}",
        "Use only spaces or only tabs in a file. Most editors can convert the whole file for you.");

    /// <summary>PIB1001's help when <c>indent_style</c> in <c>.editorconfig</c> says what the project indents with.</summary>
    /// <param name="indent"><c>spaces</c> or <c>tabs</c>.</param>
    internal static string MixedIndentationHelpWithStyle(string indent) =>
        $"Use only {indent} in this file, since `indent_style` in `.editorconfig` asks for {indent}. Most editors can convert the whole file for you.";

    /// <summary>PIB1002: a line is indented less than the line above it, but not as far back as any block it could belong to.</summary>
    public static DiagnosticDescriptor InconsistentIndentation { get; } = new(
        "PIB1002",
        DiagnosticSeverity.Error,
        "I can't tell which block this line belongs to: its indentation doesn't line up with any block above it.",
        help: "Indent it to match the block it belongs to.");

    /// <summary>PIB1003: an indented line under a line that can't have a block.</summary>
    public static DiagnosticDescriptor UnexpectedIndentation { get; } = new(
        "PIB1003",
        DiagnosticSeverity.Error,
        "This line is indented, but the line above it doesn't open a block.",
        help: "Only a block opener such as `@if`, an option (`->`) or an alternative (`- `) can have indented lines under it.");

    /// <summary>PIB1004: a block opener with no indented lines under it.</summary>
    /// <remarks>Argument: the opener's line, such as <c>@if $door_open</c>.</remarks>
    public static DiagnosticDescriptor MissingBlock { get; } = new(
        "PIB1004",
        DiagnosticSeverity.Error,
        "I expected indented lines under `{0}`.",
        help: "Put the lines it controls below it, indented.");

    /// <summary>PIB1005: a <c>///</c> note, which v1 reserves for localization.</summary>
    public static DiagnosticDescriptor UnsupportedNote { get; } = new(
        "PIB1005",
        DiagnosticSeverity.Error,
        "`///` notes aren't supported yet.",
        help: "For a comment, use `//`.");

    /// <summary>PIB1010: a markup span with no close on its line, or none before a conditional-text boundary.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor UnclosedMarkup { get; } = new(
        "PIB1010",
        DiagnosticSeverity.Error,
        "I can't find the end of this `[{0}]`.",
        help: "Close it on the same line with `[/{0}]`.");

    /// <summary>PIB1011: a markup close for an outer span while an inner one is still open.</summary>
    /// <remarks>Argument: the name of the innermost open span.</remarks>
    public static DiagnosticDescriptor MarkupOutOfOrder { get; } = new(
        "PIB1011",
        DiagnosticSeverity.Error,
        "I expected `[/{0}]` here, because `[{0}]` was opened last.",
        help: "Close markup in the reverse order you opened it: `[b][i]…[/i][/b]`.");

    /// <summary>PIB1012: a markup close with no matching open span.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor UnopenedMarkup { get; } = new(
        "PIB1012",
        DiagnosticSeverity.Error,
        "`[/{0}]` closes markup that was never opened.",
        help: "If you meant the text `[/{0}]`, put a backslash before it: `\\[/{0}]`.");

    /// <summary>PIB1013: a bracket, brace or <c>{if}</c> that isn't closed on its line.</summary>
    /// <remarks>Arguments: the missing closer, such as <c>}</c> or <c>{/if}</c>, and the opener it should close.</remarks>
    public static DiagnosticDescriptor Unclosed { get; } = new(
        "PIB1013",
        DiagnosticSeverity.Error,
        "I can't find the `{0}` that ends this `{1}`.",
        help: "Close it on the same line.");

    /// <summary>PIB1014: a backslash followed by something other than ASCII punctuation.</summary>
    /// <remarks>Argument: the backslash and the character after it.</remarks>
    public static DiagnosticDescriptor InvalidEscape { get; } = new(
        "PIB1014",
        DiagnosticSeverity.Error,
        "I don't know the escape `{0}`.",
        help: @"A backslash only goes before punctuation. To show a backslash, write `\\`.");

    /// <summary>PIB1015: text after a tag, which ends the line's text.</summary>
    /// <remarks>
    /// Arguments: the tag the text comes after; whose text it is, such as <c>the text</c>, or a <see cref="SpeakerMention"/>
    /// that reads <c>what Mira says</c>; and
    /// the line with the tag escaped, which the help shows on a line of its own.
    /// </remarks>
    public static DiagnosticDescriptor TextAfterTag { get; } = new(
        "PIB1015",
        DiagnosticSeverity.Error,
        "This text comes after a tag, but tags go at the end of the line.",
        "this starts a tag",
        "If `{0}` is part of {1}, put a backslash before the `#`:\n{2}");

    /// <summary>PIB1016: a command, <c>{w}</c> or <c>{p}</c> in an option's text.</summary>
    /// <remarks>Argument: the point, such as <c>{w}</c> or <c>{@jolt}</c>.</remarks>
    public static DiagnosticDescriptor NotInOption { get; } = new(
        "PIB1016",
        DiagnosticSeverity.Error,
        "An option's text can't contain `{0}`.",
        help: "Pauses and commands go in the indented lines under the option.");

    /// <summary>PIB1017: a point that isn't a variable, call, command or brace keyword.</summary>
    /// <remarks>Argument: the point as written.</remarks>
    public static DiagnosticDescriptor UnknownPoint { get; } = new(
        "PIB1017",
        DiagnosticSeverity.Error,
        "I don't know what `{0}` means.",
        help: "To show a variable, write `{{$name}}`. To call a function, write `{{name()}}`. If the braces are part of the text, put a backslash before the `{{`.");

    /// <summary>PIB1020: a file's only <c>@prefix</c> line, after something other than comments.</summary>
    public static DiagnosticDescriptor MisplacedPrefix { get; } = new(
        "PIB1020",
        DiagnosticSeverity.Error,
        "`@prefix` has to come first in the file.",
        help: "Move it to the top. Only comments can go above it.");

    /// <summary>PIB1021: a declaration inside a node.</summary>
    public static DiagnosticDescriptor DeclarationInNode { get; } = new(
        "PIB1021",
        DiagnosticSeverity.Error,
        "Declarations have to come before the file's first node.",
        help: "Move this above the first `==` line, or into another file.");

    /// <summary>PIB1022: a statement before the file's first node.</summary>
    public static DiagnosticDescriptor OutsideNode { get; } = new(
        "PIB1022",
        DiagnosticSeverity.Error,
        "This line isn't inside a node.",
        help: "Add a node header above it: `== name`.");

    /// <summary>PIB1023: a second <c>@prefix</c> line in a file.</summary>
    /// <remarks>Argument: the 1-based line of the file's first <c>@prefix</c>.</remarks>
    public static DiagnosticDescriptor DuplicatePrefix { get; } = new(
        "PIB1023",
        DiagnosticSeverity.Error,
        "This file already has a `@prefix`, on line {0}.",
        help: "A file has one prefix at most. To put nodes under two prefixes, split the file in two.");

    /// <summary>PIB1030: an <c>@elif</c> or <c>@else</c> with no <c>@if</c> directly above it.</summary>
    /// <remarks>Argument: the keyword, <c>@elif</c> or <c>@else</c>.</remarks>
    public static DiagnosticDescriptor StrayClause { get; } = new(
        "PIB1030",
        DiagnosticSeverity.Error,
        "This `{0}` has no `@if` to belong to.",
        help: "Put it right after the `@if` block, at the same indentation as the `@if`.");

    /// <summary>PIB1031: a line in a variation block that isn't an alternative.</summary>
    /// <remarks>Argument: the variation's keyword, such as <c>@cycle</c>.</remarks>
    public static DiagnosticDescriptor NotAlternative { get; } = new(
        "PIB1031",
        DiagnosticSeverity.Error,
        "A `{0}:` block can only hold alternatives.",
        help: "Start each alternative with `- `.");

    /// <summary>PIB1032: a block opener right after an alternative's <c>- </c>.</summary>
    /// <remarks>Argument: the opener, such as <c>@if</c> or <c>-></c>.</remarks>
    public static DiagnosticDescriptor OpenerInAlternative { get; } = new(
        "PIB1032",
        DiagnosticSeverity.Error,
        "`{0}` can't start an alternative.",
        help: "Put `-` on its own line, and the `{0}` on the line below it, indented.");

    /// <summary>PIB1033: a block opener with a <c>:</c> after it.</summary>
    /// <remarks>Arguments: the keyword, and the line up to the <c>:</c>.</remarks>
    public static DiagnosticDescriptor OpenerColon { get; } = new(
        "PIB1033",
        DiagnosticSeverity.Error,
        "I didn't expect a `:` on this `{0}` line.",
        help: "Remove it: `{1}`.");

    /// <summary>PIB1040: something the grammar doesn't allow where it appears, when no more specific code applies.</summary>
    /// <remarks>Argument: what was found, such as <c>`extra`</c> or <c>a node header</c>.</remarks>
    public static DiagnosticDescriptor Unexpected { get; } = new(
        "PIB1040",
        DiagnosticSeverity.Error,
        "I didn't expect {0} here.");

    /// <summary>PIB1041: a quoted string with no closing quote on its line.</summary>
    public static DiagnosticDescriptor UnterminatedString { get; } = new(
        "PIB1041",
        DiagnosticSeverity.Error,
        "This quoted text never ends.",
        help: "Add the closing `\"` on the same line.");

    /// <summary>PIB1042: a number followed directly by letters, digits, <c>_</c> or another <c>.</c>.</summary>
    /// <remarks>Argument: the whole malformed number.</remarks>
    public static DiagnosticDescriptor MalformedNumber { get; } = new(
        "PIB1042",
        DiagnosticSeverity.Error,
        "`{0}` isn't a number I can read.",
        help: "Write numbers with digits and at most one `.`, like `3`, `0.5` or `.5`. A duration ends in `s` or `ms`, like `0.5s`.");

    /// <summary>PIB1043: a tag name holding characters other than letters, digits and <c>_</c>.</summary>
    /// <remarks>Arguments: the tag as written, and the same tag with each such character replaced by <c>_</c>.</remarks>
    public static DiagnosticDescriptor MalformedTagName { get; } = new(
        "PIB1043",
        DiagnosticSeverity.Error,
        "`{0}` isn't a tag name: tag names only have letters, digits and `_`.",
        help: "Write `{1}`.");

    /// <summary>PIB1044: a display name holding <c>[</c>, <c>{</c> or <c>\</c>.</summary>
    public static DiagnosticDescriptor InvalidDisplayName { get; } = new(
        "PIB1044",
        DiagnosticSeverity.Error,
        @"A display name can't contain `[`, `{{` or `\`.",
        help: "Write the name as plain text.");

    /// <summary>PIB1045: a dotted or relative name where a single identifier is being declared.</summary>
    /// <remarks>Argument: the name as written.</remarks>
    public static DiagnosticDescriptor DottedName { get; } = new(
        "PIB1045",
        DiagnosticSeverity.Error,
        "`{0}` has a dot, but only node names can.",
        help: "Use a single name, with `_` between words if it needs them.");

    /// <summary>PIB1046: a required part of a line is missing.</summary>
    /// <remarks>Arguments: what's missing, such as <c>a node name</c>, and the text it should follow.</remarks>
    public static DiagnosticDescriptor Missing { get; } = new(
        "PIB1046",
        DiagnosticSeverity.Error,
        "I expected {0} after `{1}`.");

    /// <summary>PIB1050: a positional argument after a named one.</summary>
    public static DiagnosticDescriptor ArgumentOrder { get; } = new(
        "PIB1050",
        DiagnosticSeverity.Error,
        "This argument has no name, but it comes after one that does.",
        help: "Put unnamed arguments first, then named ones, then `wait` or `nowait`.");

    /// <summary>PIB1051: a space between a function's name and its <c>(</c>, where only a call can follow the name.</summary>
    /// <remarks>Argument: the function's name.</remarks>
    public static DiagnosticDescriptor SpaceBeforeCall { get; } = new(
        "PIB1051",
        DiagnosticSeverity.Error,
        "There's a space between `{0}` and its `(`.",
        help: "To call `{0}`, remove the space: `{0}(…)`.");

    /// <summary>PIB1052: an <c>#id</c> whose value isn't a line ID.</summary>
    /// <remarks>Argument: the tag as written.</remarks>
    public static DiagnosticDescriptor InvalidLineId { get; } = new(
        "PIB1052",
        DiagnosticSeverity.Error,
        "`{0}` isn't a line ID I can use.",
        help: "Line IDs are lowercase letters, digits and `_`, starting with a letter. `pibbles ids` makes them for you.");

    /// <summary>PIB1053: a tag on a line that doesn't take it.</summary>
    /// <remarks>Arguments: the tag, the kind of line it's on, and what that line takes instead.</remarks>
    public static DiagnosticDescriptor TagNotAllowed { get; } = new(
        "PIB1053",
        DiagnosticSeverity.Error,
        "`{0}` can't go on {1}.",
        help: "{2}");

    /// <summary>PIB1054: a speaker with neither a pose nor text.</summary>
    /// <remarks>Arguments: the speaker's name as written, and a <see cref="SpeakerMention"/> of it for the help's prose.</remarks>
    public static DiagnosticDescriptor EmptyLine { get; } = new(
        "PIB1054",
        DiagnosticSeverity.Error,
        "`{0}:` has nothing after it.",
        help: "Write what {1} says after the colon, or write `{0}: {{w}}` for a box with only their name. To change their pose without a line, write `{0} (pose):`.");

    /// <summary>PIB1055: a negative number as a command argument, without parentheses.</summary>
    /// <remarks>Argument: the negative value as written, such as <c>-1</c>.</remarks>
    public static DiagnosticDescriptor NegativeArgument { get; } = new(
        "PIB1055",
        DiagnosticSeverity.Error,
        "A negative argument has to go in brackets.",
        help: "Write `({0})`.");

    /// <summary>PIB1060: a <c>(</c> with no <c>)</c> on its line.</summary>
    public static DiagnosticDescriptor UnclosedParenthesis { get; } = new(
        "PIB1060",
        DiagnosticSeverity.Error,
        "I can't find the `)` that closes this `(`.",
        help: "Add the `)` on the same line.");

    /// <summary>PIB1061: two comparisons in a row, such as <c>$a &lt; $b &lt; $c</c>.</summary>
    /// <remarks>Argument: the whole chain as written.</remarks>
    public static DiagnosticDescriptor ChainedComparison { get; } = new(
        "PIB1061",
        DiagnosticSeverity.Error,
        "I can't compare three things at once: `{0}`.",
        help: "Compare two at a time, joined with `and`: `$a < $b and $b < $c`.");

    /// <summary>PIB2001: a speaker that isn't a declared actor.</summary>
    /// <remarks>
    /// Arguments: the speaker as written; a suggestion, such as <c>Did you mean `mira`? </c>, or nothing; and the line's
    /// start up to its colon, which the help escapes.
    /// </remarks>
    public static DiagnosticDescriptor UnknownSpeaker { get; } = new(
        "PIB2001",
        DiagnosticSeverity.Error,
        "I don't know an actor called `{0}`.",
        help: "{1}If this line is narration, escape the colon: `{2}\\:`.");

    /// <summary>PIB2002: a pose the speaker doesn't have.</summary>
    /// <remarks>Arguments: the actor's display name; the pose; and a suggestion or the actor's poses, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownPose { get; } = new(
        "PIB2002",
        DiagnosticSeverity.Error,
        "{0} has no pose called `{1}`.",
        help: "{2}");

    /// <summary>PIB2003: a parenthesis after a name, before a colon, that doesn't hold a single name, as in <c>mira (to Rex):</c>.</summary>
    /// <remarks>Arguments: the parenthesis, and the line's start up to its colon, which the help escapes.</remarks>
    public static DiagnosticDescriptor NotPose { get; } = new(
        "PIB2003",
        DiagnosticSeverity.Error,
        "`{0}` isn't a pose: a pose is a single name.",
        help: "For how a line is said, use a `//` comment. If this is narration, escape the colon: `{1}\\:`.");

    /// <summary>PIB2004: a declared actor and a colon with no space after it, as in <c>mira:Hi</c>.</summary>
    /// <remarks>Arguments: the line's start up to its colon, and the line with its colon escaped.</remarks>
    public static DiagnosticDescriptor NoSpaceAfterSpeaker { get; } = new(
        "PIB2004",
        DiagnosticSeverity.Warning,
        "There's no space after `{0}:`.",
        help: "Add one, or escape the colon if this is narration: `{1}`.");

    /// <summary>PIB2010: a command that isn't declared.</summary>
    /// <remarks>Arguments: the command's name, without its <c>@</c>, and the closest command, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownCommand { get; } = new(
        "PIB2010",
        DiagnosticSeverity.Error,
        "I don't know a command called `@{0}`.",
        help: "Did you mean `@{1}`?");

    /// <summary>PIB2011: an argument whose type isn't the type of its parameter.</summary>
    /// <remarks>
    /// Arguments: what takes the argument, such as <c>@show</c> or <c>has_item()</c>; the parameter's type with its
    /// article; the parameter; the argument's type with its article; and what values to use instead, or
    /// <see langword="null"/> to leave out the help.
    /// </remarks>
    public static DiagnosticDescriptor ArgumentType { get; } = new(
        "PIB2011",
        DiagnosticSeverity.Error,
        "`{0}` expects {1} for `{2}`, but this is {3}.",
        help: "{4}");

    /// <summary>PIB2012: a command inside a line that isn't declared <c>inline</c>.</summary>
    /// <remarks>Argument: the command's name, without its <c>@</c>.</remarks>
    public static DiagnosticDescriptor NotInline { get; } = new(
        "PIB2012",
        DiagnosticSeverity.Error,
        "`@{0}` can't be used inside a line, because it isn't declared `inline`.",
        help: "Put it on its own `@` line.");

    /// <summary>PIB2013: more arguments than parameters.</summary>
    /// <remarks>Arguments: what takes the arguments, such as <c>has_item()</c>, and how many it takes, such as <c>only 1 argument</c>.</remarks>
    public static DiagnosticDescriptor TooManyArguments { get; } = new(
        "PIB2013",
        DiagnosticSeverity.Error,
        "`{0}` takes {1}.",
        help: "Remove the extra ones.");

    /// <summary>PIB2014: a named argument for a parameter that doesn't exist.</summary>
    /// <remarks>Arguments: what takes the arguments, such as <c>@show</c>; the name; and the closest parameter, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownParameter { get; } = new(
        "PIB2014",
        DiagnosticSeverity.Error,
        "`{0}` has no parameter called `{1}`.",
        help: "Did you mean `{2}`?");

    /// <summary>PIB2015: a parameter with no default and no argument.</summary>
    /// <remarks>Arguments: what takes the arguments; the missing parameters, such as <c>a value for `to`</c>; and how to add them.</remarks>
    public static DiagnosticDescriptor MissingArgument { get; } = new(
        "PIB2015",
        DiagnosticSeverity.Error,
        "`{0}` needs {1}.",
        help: "{2}");

    /// <summary>PIB2016: a named argument for a parameter that already has one.</summary>
    /// <remarks>Arguments: what takes the arguments, and the parameter.</remarks>
    public static DiagnosticDescriptor RepeatedArgument { get; } = new(
        "PIB2016",
        DiagnosticSeverity.Error,
        "`{0}` already has a value for `{1}`.",
        help: "Remove one of them.");

    /// <summary>PIB2017: markup that isn't declared or built in.</summary>
    /// <remarks>Arguments: the markup's name, and the closest markup, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownMarkup { get; } = new(
        "PIB2017",
        DiagnosticSeverity.Error,
        "I don't know markup called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2018: an icon that isn't declared.</summary>
    /// <remarks>Arguments: the icon's name, and the closest icon, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownIcon { get; } = new(
        "PIB2018",
        DiagnosticSeverity.Error,
        "I don't know an icon called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2019: a call to a function that isn't declared.</summary>
    /// <remarks>Arguments: the function's name, and the closest function, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownFunction { get; } = new(
        "PIB2019",
        DiagnosticSeverity.Error,
        "I don't know a function called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2020: a node name that names no node.</summary>
    /// <remarks>Arguments: the name as written, and the closest node, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownNode { get; } = new(
        "PIB2020",
        DiagnosticSeverity.Error,
        "I can't find a node called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2021: a relative node name in a file with no <c>@prefix</c>.</summary>
    /// <remarks>Argument: the name as written, such as <c>.leave</c>.</remarks>
    public static DiagnosticDescriptor RelativeWithoutPrefix { get; } = new(
        "PIB2021",
        DiagnosticSeverity.Error,
        "`{0}` is relative, but this file has no `@prefix`.",
        help: "Write the full name, or add a `@prefix` at the top of the file.");

    /// <summary>PIB2022: a node name or <c>#was:</c> name that another node, or the same one, already uses.</summary>
    /// <remarks>Arguments: the full name, and where it's already used, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor DuplicateNode { get; } = new(
        "PIB2022",
        DiagnosticSeverity.Error,
        "There's already a node called `{0}`, {1}.",
        help: "A node's name, and each old name in its `#was:`, can only be used once in a story. Rename one of them.");

    /// <summary>PIB2023: a <c>@prefix</c> that starts with a dot, which does nothing.</summary>
    /// <remarks>Arguments: the prefix as written, and without its dot.</remarks>
    public static DiagnosticDescriptor RelativePrefix { get; } = new(
        "PIB2023",
        DiagnosticSeverity.Info,
        "`@prefix {0}` doesn't need its dot.",
        help: "Remove it: `@prefix {1}`.");

    /// <summary>PIB2030: a variable that isn't declared.</summary>
    /// <remarks>Arguments: the variable, with its <c>$</c>, and the closest variable, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownVariable { get; } = new(
        "PIB2030",
        DiagnosticSeverity.Error,
        "I don't know a variable called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2031: a value whose type isn't the type where it's used: a variable's value, a default, or <c>@wait</c>'s time.</summary>
    /// <remarks>
    /// Arguments: what the value is for, such as <c>`$has_key` holds a `bool`</c>; the value's type with its article; and
    /// what values to use instead, or <see langword="null"/> to leave out the help.
    /// </remarks>
    public static DiagnosticDescriptor ValueType { get; } = new(
        "PIB2031",
        DiagnosticSeverity.Error,
        "{0}, but this is {1}.",
        help: "{2}");

    /// <summary>PIB2032: a condition that isn't a <c>bool</c>.</summary>
    /// <remarks>Arguments: the condition as written; its type with its article; and a condition that's probably meant, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NotCondition { get; } = new(
        "PIB2032",
        DiagnosticSeverity.Error,
        "`{0}` is {1}, but a condition has to be true or false.",
        help: "{2}");

    /// <summary>PIB2033: an operator used on types it doesn't work on.</summary>
    /// <remarks>Arguments: the operator; the operands' types, such as <c>a `string` and a `number`</c>; and what the operator works on.</remarks>
    public static DiagnosticDescriptor OperatorTypes { get; } = new(
        "PIB2033",
        DiagnosticSeverity.Error,
        "I can't use `{0}` with {1}.",
        help: "{2}");

    /// <summary>PIB2034: a bare name where no name can go, such as a whole condition or an operand of <c>+</c>.</summary>
    /// <remarks>Arguments: the name, and what was probably meant, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NameNotAllowed { get; } = new(
        "PIB2034",
        DiagnosticSeverity.Error,
        "I don't know what `{0}` means here.",
        help: "{1}");

    /// <summary>PIB2036: a bare name that isn't a value of the type expected where it appears.</summary>
    /// <remarks>Arguments: the name; the type with its article; and a suggestion, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NotValueOfType { get; } = new(
        "PIB2036",
        DiagnosticSeverity.Error,
        "`{0}` isn't {1}.",
        help: "{2}");

    /// <summary>PIB2037: <c>==</c> or <c>!=</c> between two bare names, which have no type to be read against.</summary>
    /// <remarks>Argument: the comparison as written.</remarks>
    public static DiagnosticDescriptor TwoNames { get; } = new(
        "PIB2037",
        DiagnosticSeverity.Error,
        "I can't compare two names: `{0}`.",
        help: "Compare a variable with a name instead, like `$where == left`.");

    /// <summary>PIB2038: a value shown in text whose type isn't text, a number or an actor.</summary>
    /// <remarks>Arguments: the value as written; its type with its article; and how to show text that depends on it.</remarks>
    public static DiagnosticDescriptor NotShowable { get; } = new(
        "PIB2038",
        DiagnosticSeverity.Error,
        "I can't show `{0}` in text, because it's {1}.",
        help: "{2}");

    /// <summary>PIB2045: a tag that's neither reserved nor declared.</summary>
    /// <remarks>Arguments: the tag's name, and a suggestion, such as <c>Did you mean `#thought`? </c>, or nothing.</remarks>
    public static DiagnosticDescriptor UnknownTag { get; } = new(
        "PIB2045",
        DiagnosticSeverity.Error,
        "I don't know a tag called `#{0}`.",
        help: "{1}If this is text, escape it: `\\#{0}`.");

    /// <summary>PIB2046: a value on a tag that takes none, or none on a tag that needs one.</summary>
    /// <remarks>Arguments: the tag's name; <c>doesn't take a value</c> or <c>needs a value</c>; and the tag written the right way.</remarks>
    public static DiagnosticDescriptor TagValuePresence { get; } = new(
        "PIB2046",
        DiagnosticSeverity.Error,
        "`#{0}` {1}.",
        help: "{2}");

    /// <summary>PIB2047: a reference to a node by one of its old names.</summary>
    /// <remarks>Arguments: the old name, and the current name, both as the story would write them.</remarks>
    public static DiagnosticDescriptor OldNodeName { get; } = new(
        "PIB2047",
        DiagnosticSeverity.Warning,
        "`{0}` is an old name of `{1}`.",
        help: "Use the current name: `{1}`.");

    /// <summary>PIB2048: a tag's value that isn't a member of the enum the tag takes.</summary>
    /// <remarks>Arguments: the value; the enum with its article; the tag's name; and a suggestion or the enum's members, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor TagValueNotMember { get; } = new(
        "PIB2048",
        DiagnosticSeverity.Error,
        "`{0}` isn't {1}, which `#{2}` takes.",
        help: "{3}");

    /// <summary>PIB2060: a declared name that another declaration of the same kind already uses.</summary>
    /// <remarks>Arguments: the kind with its article, such as <c>an enum</c>; the name; and where the first one is, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor DuplicateDeclaration { get; } = new(
        "PIB2060",
        DiagnosticSeverity.Error,
        "There's already {0} called `{1}`, {2}.",
        help: "Give one of them another name, or remove one.");

    /// <summary>PIB2061: a pose, enum member or parameter listed twice in one declaration.</summary>
    /// <remarks>Arguments: the name, and the list it's in, such as <c>Mira's poses</c>.</remarks>
    public static DiagnosticDescriptor RepeatedName { get; } = new(
        "PIB2061",
        DiagnosticSeverity.Error,
        "`{0}` is already one of {1}.",
        help: "Remove the second `{0}`, or give it another name.");

    /// <summary>PIB2062: a declared name that's a reserved word for its kind.</summary>
    /// <remarks>Arguments: the name; the kind with its article, such as <c>a command</c>; and where Pibbles uses the word, such as <c>after `@`</c>.</remarks>
    public static DiagnosticDescriptor ReservedName { get; } = new(
        "PIB2062",
        DiagnosticSeverity.Error,
        "`{0}` can't name {1}, because Pibbles uses that word {2}.",
        help: "Choose another name.");

    /// <summary>PIB2063: a declared name with a character outside ASCII.</summary>
    /// <remarks>Arguments: the name, and its first character outside ASCII.</remarks>
    public static DiagnosticDescriptor NonAsciiName { get; } = new(
        "PIB2063",
        DiagnosticSeverity.Error,
        "`{0}` has `{1}` in it, but a name can only use English letters, digits and `_`.",
        help: "Replace `{1}` with a letter from a to z.");

    /// <summary>PIB2064: a type that's neither built in nor a declared enum.</summary>
    /// <remarks>Arguments: the type as written, and the closest type, which the help suggests. The help is left out when nothing is close.</remarks>
    public static DiagnosticDescriptor UnknownType { get; } = new(
        "PIB2064",
        DiagnosticSeverity.Error,
        "I don't know a type called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2065: a parameter with no default after one that has a default.</summary>
    /// <remarks>Argument: the parameter's name.</remarks>
    public static DiagnosticDescriptor RequiredAfterOptional { get; } = new(
        "PIB2065",
        DiagnosticSeverity.Error,
        "`{0}` has no default, but it comes after a parameter that has one.",
        help: "Put the parameters that have defaults last.");

    /// <summary>PIB2066: a tag whose value type is neither <c>string</c> nor an enum.</summary>
    /// <remarks>Argument: the type.</remarks>
    public static DiagnosticDescriptor TagValueType { get; } = new(
        "PIB2066",
        DiagnosticSeverity.Error,
        "A tag's value is text, so it can't be `{0}`.",
        help: "Use `string`, or an enum to allow only certain values.");

    /// <summary>PIB2067: a variable whose initial value is a name, with no type written.</summary>
    /// <remarks>
    /// Arguments: the variable, with its <c>$</c>; the initial value; and the declaration with its type written out. The
    /// help is left out when the value names no enum member, actor or node, or more than one kind of them.
    /// </remarks>
    public static DiagnosticDescriptor UntypedVariable { get; } = new(
        "PIB2067",
        DiagnosticSeverity.Error,
        "`{0}` starts as `{1}`, so I need its type written out.",
        help: "Write the type after the variable: `{2}`.");

    /// <summary>PIB3001: a statement after one that always leaves its block.</summary>
    /// <remarks>Arguments: why it never runs, such as <c>of the `@jump` above it</c>, and the statement that leaves, such as <c>@jump</c>.</remarks>
    public static DiagnosticDescriptor NeverRuns { get; } = new(
        "PIB3001",
        DiagnosticSeverity.Warning,
        "This line never runs, because {0}.",
        help: "Remove it, or move it above the `{1}`.");

    /// <summary>PIB3002: an option with no text.</summary>
    public static DiagnosticDescriptor EmptyOption { get; } = new(
        "PIB3002",
        DiagnosticSeverity.Info,
        "This option has no text.",
        help: "Write what the player picks after the `->`.");

    /// <summary>PIB3010: a line that needs a line ID and has none.</summary>
    public static DiagnosticDescriptor MissingLineId { get; } = new(
        "PIB3010",
        DiagnosticSeverity.Warning,
        "This line has no `#id`.",
        help: "Run `pibbles ids` to add one.");

    /// <summary>PIB3011: a line ID that another line already uses.</summary>
    /// <remarks>Arguments: the ID, and where it's already used, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor DuplicateLineId { get; } = new(
        "PIB3011",
        DiagnosticSeverity.Error,
        "The line ID `{0}` is also used {1}.",
        help: "Delete one of the two IDs and run `pibbles ids` to give that line a new one.");

    /// <summary>PIB3012: a line ID that's also a node's name or old name.</summary>
    /// <remarks>Arguments: the ID, and what it also is, such as <c>the name of a node</c>.</remarks>
    public static DiagnosticDescriptor LineIdIsNodeName { get; } = new(
        "PIB3012",
        DiagnosticSeverity.Error,
        "The line ID `{0}` is also {1}.",
        help: "Delete the ID and run `pibbles ids` to give the line a new one.");

    /// <summary>PIB5001: a block nested deeper than <c>pibbles_max_nesting</c>.</summary>
    /// <remarks>Arguments: how many blocks deep the line is, and the limit.</remarks>
    public static DiagnosticDescriptor DeepNesting { get; } = new(
        "PIB5001",
        DiagnosticSeverity.Hint,
        "This line is {0} blocks deep, and more than {1} gets hard to follow.",
        help: "Move the inner blocks into a node of their own and `@call` it, or combine conditions with `and`.");

    /// <summary>PIB5002: an option whose body is longer than <c>pibbles_max_option_body</c> lines.</summary>
    /// <remarks>Arguments: the body's length in lines, and the limit.</remarks>
    public static DiagnosticDescriptor LongOptionBody { get; } = new(
        "PIB5002",
        DiagnosticSeverity.Hint,
        "This option's body is {0} lines long, more than {1}, so the rest of the choice ends up far from it.",
        help: "Move the body into a node of its own and `@jump` to it.");

    /// <summary>PIB5003: the same speaker saying the same text in at least <c>pibbles_min_repeated_lines</c> places.</summary>
    /// <remarks>Arguments: who says it, as a <see cref="SpeakerMention"/> or a phrase for narration, and in how many places.</remarks>
    public static DiagnosticDescriptor RepeatedLine { get; } = new(
        "PIB5003",
        DiagnosticSeverity.Hint,
        "{0} in {1} places.",
        help: "Put it in a node of its own and `@call` that node from each place, so it's translated and recorded once.");

    /// <summary>PIB5004: the same <c>[color]</c> value in at least <c>pibbles_min_repeated_colors</c> places.</summary>
    /// <remarks>Arguments: the color, and in how many places it's used.</remarks>
    public static DiagnosticDescriptor RepeatedColor { get; } = new(
        "PIB5004",
        DiagnosticSeverity.Hint,
        "The color `{0}` is used in {1} places.",
        help: "If it means something, such as a clue, give it a name: declare a markup such as `@markup clue`, ask whoever programs the game to style it, and use `[clue]` in each place.");

    /// <summary>PIB5010: a pose change nobody sees.</summary>
    /// <remarks>Arguments: why, as a <see cref="SpeakerMention"/>, and the fix.</remarks>
    public static DiagnosticDescriptor RedundantPose { get; } = new(
        "PIB5010",
        DiagnosticSeverity.Hint,
        "This pose change does nothing, because {0}.",
        help: "{1}");

    /// <summary>PIB5011: a pause that does nothing, or two that could be one.</summary>
    /// <remarks>Arguments: what's wrong with the pause, and the fix.</remarks>
    public static DiagnosticDescriptor RedundantPause { get; } = new(
        "PIB5011",
        DiagnosticSeverity.Hint,
        "This pause {0}.",
        help: "{1}");

    /// <summary>PIB5012: a markup span that does nothing.</summary>
    /// <remarks>Arguments: the markup's name, what's wrong with the span, and the fix.</remarks>
    public static DiagnosticDescriptor RedundantMarkup { get; } = new(
        "PIB5012",
        DiagnosticSeverity.Hint,
        "This `[{0}]` {1}.",
        help: "{2}");

    /// <summary>PIB5013: an <c>@if</c> or <c>{if}</c> whose branches are all the same.</summary>
    /// <remarks>Argument: <c>@if</c> or <c>{if}</c>.</remarks>
    public static DiagnosticDescriptor RedundantCondition { get; } = new(
        "PIB5013",
        DiagnosticSeverity.Hint,
        "Every branch of this `{0}` is the same, so the condition makes no difference.",
        help: "Keep one copy of the branch, without the condition.");

    /// <summary>PIB5014: <c>@return</c> as the last statement of a node.</summary>
    public static DiagnosticDescriptor RedundantReturn { get; } = new(
        "PIB5014",
        DiagnosticSeverity.Hint,
        "This `@return` does nothing, since the end of a node returns anyway.",
        help: "Remove it.");

    /// <summary>PIB5020: a comparison with <c>true</c> or <c>false</c>.</summary>
    /// <remarks>Arguments: the literal, and the condition without the comparison.</remarks>
    public static DiagnosticDescriptor ComparisonWithBool { get; } = new(
        "PIB5020",
        DiagnosticSeverity.Hint,
        "Comparing with `{0}` isn't needed.",
        help: "Write `{1}`.");

    /// <summary>PIB5021: <c>@set $x = $x + v</c>, which <c>+=</c> or <c>-=</c> says more briefly.</summary>
    /// <remarks>Arguments: the operator, and the line written with it.</remarks>
    public static DiagnosticDescriptor CompoundAssignment { get; } = new(
        "PIB5021",
        DiagnosticSeverity.Hint,
        "This can be shorter with `{0}`.",
        help: "Write `{1}`.");

    /// <summary>PIB5030: a line or option longer than <c>pibbles_max_message_length</c> or <c>pibbles_max_option_length</c>.</summary>
    /// <remarks>Arguments: <c>line</c> or <c>option</c>, its length, the limit, and the fix.</remarks>
    public static DiagnosticDescriptor LongText { get; } = new(
        "PIB5030",
        DiagnosticSeverity.Hint,
        "This {0} is {1} characters long, more than {2}.",
        help: "{3}");

    /// <summary>PIB5031: a declared name that isn't <c>snake_case</c>.</summary>
    /// <remarks>Arguments: the name as written, and in <c>snake_case</c>.</remarks>
    public static DiagnosticDescriptor NamingConvention { get; } = new(
        "PIB5031",
        DiagnosticSeverity.Hint,
        "`{0}` isn't written in snake_case.",
        help: "Write it as `{1}`, before anything outside the story uses the name.");

    /// <summary>PIB5032: a block indented differently from the file's other blocks, or from <c>.editorconfig</c>.</summary>
    /// <remarks>Arguments: how the block is indented, what it should match, and how to indent it.</remarks>
    public static DiagnosticDescriptor IndentationWidth { get; } = new(
        "PIB5032",
        DiagnosticSeverity.Hint,
        "This block is indented {0}, but {1}.",
        help: "Indent it {2}.");

    /// <summary>PIB5033: a speaker written with spacing other than <c>name (pose):</c>.</summary>
    /// <remarks>Argument: the speaker written the usual way.</remarks>
    public static DiagnosticDescriptor SpeakerSpacing { get; } = new(
        "PIB5033",
        DiagnosticSeverity.Hint,
        "This speaker is spaced differently from the usual `{0}`.",
        help: "Write `{0}`.");

    /// <summary>PIB5040: a variable nothing in the story uses.</summary>
    /// <remarks>Argument: the variable's name, without the <c>$</c>.</remarks>
    public static DiagnosticDescriptor UnusedVariable { get; } = new(
        "PIB5040",
        DiagnosticSeverity.Info,
        "Nothing in the story uses `${0}`.",
        help: "Remove its `@var`, or use it.");

    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MixedIndentation,
        InconsistentIndentation,
        UnexpectedIndentation,
        MissingBlock,
        UnsupportedNote,
        UnclosedMarkup,
        MarkupOutOfOrder,
        UnopenedMarkup,
        Unclosed,
        InvalidEscape,
        TextAfterTag,
        NotInOption,
        UnknownPoint,
        MisplacedPrefix,
        DeclarationInNode,
        OutsideNode,
        DuplicatePrefix,
        StrayClause,
        NotAlternative,
        OpenerInAlternative,
        OpenerColon,
        Unexpected,
        UnterminatedString,
        MalformedNumber,
        MalformedTagName,
        InvalidDisplayName,
        DottedName,
        Missing,
        ArgumentOrder,
        SpaceBeforeCall,
        InvalidLineId,
        TagNotAllowed,
        EmptyLine,
        NegativeArgument,
        UnclosedParenthesis,
        ChainedComparison,
        UnknownSpeaker,
        UnknownPose,
        NotPose,
        NoSpaceAfterSpeaker,
        UnknownCommand,
        ArgumentType,
        NotInline,
        TooManyArguments,
        UnknownParameter,
        MissingArgument,
        RepeatedArgument,
        UnknownMarkup,
        UnknownIcon,
        UnknownFunction,
        UnknownNode,
        RelativeWithoutPrefix,
        DuplicateNode,
        RelativePrefix,
        UnknownVariable,
        ValueType,
        NotCondition,
        OperatorTypes,
        NameNotAllowed,
        NotValueOfType,
        TwoNames,
        NotShowable,
        UnknownTag,
        TagValuePresence,
        OldNodeName,
        TagValueNotMember,
        DuplicateDeclaration,
        RepeatedName,
        ReservedName,
        NonAsciiName,
        UnknownType,
        RequiredAfterOptional,
        TagValueType,
        UntypedVariable,
        NeverRuns,
        EmptyOption,
        MissingLineId,
        DuplicateLineId,
        LineIdIsNodeName,
        DeepNesting,
        LongOptionBody,
        RepeatedLine,
        RepeatedColor,
        RedundantPose,
        RedundantPause,
        RedundantMarkup,
        RedundantCondition,
        RedundantReturn,
        ComparisonWithBool,
        CompoundAssignment,
        LongText,
        NamingConvention,
        IndentationWidth,
        SpeakerSpacing,
        UnusedVariable,
    ];
}
