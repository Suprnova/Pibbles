namespace Pibbles.Diagnostics;

/// <summary>How serious a diagnostic is, from least to most.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Pure style. Editors show it faintly, and the CLI shows it only with <c>--style</c>.</summary>
    Hint,

    /// <summary>Worth knowing, but never a problem.</summary>
    Info,

    /// <summary>Probably a mistake. The story still compiles.</summary>
    Warning,

    /// <summary>A mistake the story can't compile with.</summary>
    Error,
}
