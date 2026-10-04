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
        help: "Only a line ending in `:`, an option (`->`) or an alternative (`- `) can have indented lines under it.");

    /// <summary>PIB1004: a block opener with no indented lines under it.</summary>
    /// <remarks>Argument: the opener's line, such as <c>@if $door_open:</c>.</remarks>
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

    /// <summary>PIB1014: a backslash followed by something other than ASCII punctuation.</summary>
    /// <remarks>Argument: the backslash and the character after it.</remarks>
    public static DiagnosticDescriptor InvalidEscape { get; } = new(
        "PIB1014",
        DiagnosticSeverity.Error,
        "I don't know the escape `{0}`.",
        help: @"A backslash only goes before punctuation. To show a backslash, write `\\`.");

    /// <summary>PIB1020: a file's only <c>@prefix</c> line, after something other than comments.</summary>
    public static DiagnosticDescriptor MisplacedPrefix { get; } = new(
        "PIB1020",
        DiagnosticSeverity.Error,
        "`@prefix` has to come first in the file.",
        help: "Move it to the top. Only comments can go above it.");

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

    /// <summary>PIB1033: a block opener without its <c>:</c>.</summary>
    /// <remarks>Arguments: the keyword, and the line as written.</remarks>
    public static DiagnosticDescriptor MissingColon { get; } = new(
        "PIB1033",
        DiagnosticSeverity.Error,
        "I expected a `:` at the end of this `{0}` line.",
        help: "Add it: `{1}:`.");

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

    /// <summary>PIB1053: a tag on a line that doesn't take it.</summary>
    /// <remarks>Arguments: the tag, the kind of line it's on, and what that line takes instead.</remarks>
    public static DiagnosticDescriptor TagNotAllowed { get; } = new(
        "PIB1053",
        DiagnosticSeverity.Error,
        "`{0}` can't go on {1}.",
        help: "{2}");

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

    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MixedIndentation,
        InconsistentIndentation,
        UnexpectedIndentation,
        MissingBlock,
        UnsupportedNote,
        InvalidEscape,
        MisplacedPrefix,
        OutsideNode,
        DuplicatePrefix,
        StrayClause,
        MissingColon,
        Unexpected,
        UnterminatedString,
        MalformedNumber,
        MalformedTagName,
        Missing,
        ArgumentOrder,
        SpaceBeforeCall,
        TagNotAllowed,
        NegativeArgument,
        UnclosedParenthesis,
        ChainedComparison,
    ];
}
