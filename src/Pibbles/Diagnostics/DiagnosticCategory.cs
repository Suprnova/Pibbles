namespace Pibbles.Diagnostics;

/// <summary>
/// A group of codes that writers switch on or off together, given by a code's first digit. <c>.editorconfig</c> can set a
/// whole category's severity, <c>// pibbles-ignore</c> silences only style and spelling, and a line with an error drops
/// its style codes. A code starting with 1 to 3 has no category: it's one of the story's own checks.
/// </summary>
public enum DiagnosticCategory
{
    /// <summary>Translations and voice: <c>PIB4xxx</c>.</summary>
    Localization,

    /// <summary>Stories that work, but could be clearer, easier to translate or easier to maintain: <c>PIB5xxx</c>.</summary>
    Style,

    /// <summary>Unknown words: <c>PIB6xxx</c>.</summary>
    Spelling,
}
