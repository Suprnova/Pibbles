using Pibbles.Diagnostics;

namespace Pibbles.Configuration;

/// <summary>
/// The settings for one source file, from the <c>.editorconfig</c> files that apply to it. The core reads no files:
/// hosts resolve a file's properties with an <c>.editorconfig</c> library and pass them in.
/// </summary>
public sealed class FileSettings
{
    /// <summary>The diagnostic categories, by code range: PIB1xxx is <c>syntax</c>, PIB2xxx <c>binding</c>, and so on.</summary>
    internal static readonly string[] Categories = ["syntax", "binding", "content", "localization", "style", "spelling"];

    private static readonly string[] Severities = ["error", "warning", "info", "hint", "none"];

    private FileSettings(IReadOnlyDictionary<string, string> properties, IReadOnlyList<string> problems)
    {
        Properties = properties;
        Problems = problems;
    }

    /// <summary>No settings: every diagnostic keeps its default severity.</summary>
    public static FileSettings None { get; } = new(new Dictionary<string, string>(), []);

    /// <summary>The file's properties, with keys in lowercase, and the values of Pibbles' own settings in lowercase too.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>
    /// What's wrong with the file's <c>pibbles_diagnostic</c> settings: ones that name no diagnostic or category, or give
    /// a severity that doesn't exist. Those settings are ignored.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Reads a source file's resolved <c>.editorconfig</c> properties.</summary>
    /// <param name="properties">Every property that applies to the file, after nearer files and later sections have overridden earlier ones.</param>
    public static FileSettings From(IEnumerable<KeyValuePair<string, string>> properties)
    {
        Dictionary<string, string> settings = [];
        List<string> problems = [];
        foreach ((string name, string raw) in properties)
        {
            string key = name.ToLowerInvariant();
            string value = key.StartsWith("pibbles_", StringComparison.Ordinal) ? raw.ToLowerInvariant() : raw;
            if (Check(key, value) is { } problem)
                problems.Add(problem);
            else
                settings[key] = value;
        }

        return new(settings, problems);
    }

    /// <summary>
    /// Applies the file's settings to a diagnostic. It gets the severity its code's setting asks for, or else its
    /// category's, and <see langword="null"/> is returned when that's <c>none</c>, which turns the diagnostic off. Mixed
    /// indentation (PIB1001) says which character to use when <c>indent_style</c> does.
    /// </summary>
    internal Diagnostic? Configure(Diagnostic diagnostic)
    {
        if (diagnostic.Code == DiagnosticCatalog.MixedIndentation.Code && IndentWith() is { } indent)
            diagnostic = diagnostic with { Help = DiagnosticCatalog.MixedIndentationHelpWithStyle(indent) };

        string category = Categories[diagnostic.Code[3] - '1'];
        string? severity = Properties.GetValueOrDefault($"pibbles_diagnostic.{diagnostic.Code.ToLowerInvariant()}.severity")
            ?? Properties.GetValueOrDefault($"pibbles_diagnostic.category-{category}.severity");

        return severity switch
        {
            "none" => null,
            "error" => diagnostic with { Severity = DiagnosticSeverity.Error },
            "warning" => diagnostic with { Severity = DiagnosticSeverity.Warning },
            "info" => diagnostic with { Severity = DiagnosticSeverity.Info },
            "hint" => diagnostic with { Severity = DiagnosticSeverity.Hint },
            _ => diagnostic,
        };
    }

    /// <summary>What <c>indent_style</c> asks a file to indent with, <c>spaces</c> or <c>tabs</c>, or <see langword="null"/> if it isn't set.</summary>
    private string? IndentWith() => Properties.GetValueOrDefault("indent_style")?.ToLowerInvariant() switch
    {
        "space" => "spaces",
        "tab" => "tabs",
        _ => null,
    };

    /// <summary>Checks a <c>pibbles_diagnostic</c> setting: <c>pibbles_diagnostic.&lt;code or category-name&gt;.severity</c>.</summary>
    private static string? Check(string key, string value)
    {
        if (!key.StartsWith("pibbles_diagnostic.", StringComparison.Ordinal))
            return null;

        if (key.Split('.') is not [_, var target, "severity"])
            return $"I don't know the setting `{key}`. To change a diagnostic's severity, write `pibbles_diagnostic.PIB5003.severity`, or `pibbles_diagnostic.category-style.severity` for a whole category.";

        bool known = target.StartsWith("category-", StringComparison.Ordinal)
            ? Categories.Contains(target["category-".Length..])
            : DiagnosticCatalog.All.Any(descriptor => descriptor.Code.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (!known)
            return $"`{key}` names no diagnostic code or category. The categories are {string.Join(", ", Categories.Select(category => $"`category-{category}`"))}.";

        return Severities.Contains(value) ? null : $"`{key} = {value}`: `{value}` isn't a severity. Use `error`, `warning`, `info`, `hint` or `none`.";
    }
}
