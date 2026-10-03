namespace Pibbles.Diagnostics;

/// <summary>A problem found in a story, with where it is and how to fix it.</summary>
/// <param name="Code">The catalog code, such as <c>PIB1001</c>.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Location">The marked text.</param>
/// <param name="Message">A plain sentence saying what's wrong.</param>
/// <param name="Label">A short note shown under the marked text, or <see langword="null"/>.</param>
/// <param name="Help">How to fix the problem, or <see langword="null"/>.</param>
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, SourceLocation Location, string Message, string? Label, string? Help);
