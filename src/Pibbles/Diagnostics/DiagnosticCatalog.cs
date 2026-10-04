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

    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MixedIndentation,
        InconsistentIndentation,
        UnsupportedNote,
        InvalidEscape,
        UnterminatedString,
        MalformedNumber,
        MalformedTagName,
    ];
}
