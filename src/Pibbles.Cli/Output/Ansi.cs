using Pibbles.Diagnostics;

namespace Pibbles.Cli.Output;

/// <summary>The terminal colors of the CLI's readable output: severities in their colors, structure in blue, fixes in green.</summary>
internal static class Ansi
{
    public const string Bold = "1";
    public const string Blue = "1;34";
    public const string Green = "1;32";

    /// <summary>The plain green of example code, such as a fixed line under a help.</summary>
    public const string Example = "32";

    public static string Paint(string text, string codes, bool color) => color ? $"\e[{codes}m{text}\e[0m" : text;

    /// <summary>A severity's color, in bold: errors red, warnings yellow, notes cyan, and hints plain.</summary>
    public static string Tint(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "1;31",
        DiagnosticSeverity.Warning => "1;33",
        DiagnosticSeverity.Info => "1;36",
        _ => Bold,
    };
}
