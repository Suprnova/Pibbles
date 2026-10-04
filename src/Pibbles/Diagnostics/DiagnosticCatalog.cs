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

    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } = [MixedIndentation, InconsistentIndentation, UnsupportedNote];
}
