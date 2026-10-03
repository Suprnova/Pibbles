using System.Globalization;

namespace Pibbles.Diagnostics;

/// <summary>One code's entry in the <see cref="DiagnosticCatalog"/>: its default severity and the templates its diagnostics are written from.</summary>
/// <remarks>
/// Templates use composite format placeholders (<c>{0}</c>), and every template of a descriptor shares the same arguments.
/// </remarks>
/// <param name="code">The code, such as <c>PIB1001</c>.</param>
/// <param name="defaultSeverity">The severity, unless configuration overrides it.</param>
/// <param name="message">The template for <see cref="Diagnostic.Message"/>.</param>
/// <param name="label">The template for <see cref="Diagnostic.Label"/>, if diagnostics with this code have one.</param>
/// <param name="help">The template for <see cref="Diagnostic.Help"/>, if diagnostics with this code have one.</param>
public sealed class DiagnosticDescriptor(string code, DiagnosticSeverity defaultSeverity, string message, string? label = null, string? help = null)
{
    /// <summary>The code, such as <c>PIB1001</c>.</summary>
    public string Code { get; } = code;

    /// <summary>The severity unless configuration overrides it.</summary>
    public DiagnosticSeverity DefaultSeverity { get; } = defaultSeverity;

    /// <summary>The template for <see cref="Diagnostic.Message"/>.</summary>
    public string Message { get; } = message;

    /// <summary>The template for <see cref="Diagnostic.Label"/>, or <see langword="null"/>.</summary>
    public string? Label { get; } = label;

    /// <summary>The template for <see cref="Diagnostic.Help"/>, or <see langword="null"/>.</summary>
    public string? Help { get; } = help;

    internal Diagnostic Create(SourceLocation location, params object?[] arguments) =>
        new(Code, DefaultSeverity, location, Format(Message, arguments)!, Format(Label, arguments), Format(Help, arguments));

    private static string? Format(string? template, object?[] arguments) =>
        template is null ? null : string.Format(CultureInfo.InvariantCulture, template, arguments);
}
