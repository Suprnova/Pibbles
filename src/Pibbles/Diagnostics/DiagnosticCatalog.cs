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

    /// <summary>PIB1053: a tag on a line that doesn't take it.</summary>
    /// <remarks>Arguments: the tag, the kind of line it's on, and what that line takes instead.</remarks>
    public static DiagnosticDescriptor TagNotAllowed { get; } = new(
        "PIB1053",
        DiagnosticSeverity.Error,
        "`{0}` can't go on {1}.",
        help: "{2}");

    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MixedIndentation,
        InconsistentIndentation,
        UnexpectedIndentation,
        UnsupportedNote,
        InvalidEscape,
        MisplacedPrefix,
        OutsideNode,
        DuplicatePrefix,
        Unexpected,
        UnterminatedString,
        MalformedNumber,
        MalformedTagName,
        Missing,
        TagNotAllowed,
    ];
}
