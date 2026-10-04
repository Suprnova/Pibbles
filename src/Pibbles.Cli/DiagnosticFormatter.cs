using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Cli;

/// <summary>Writes diagnostics in each of <c>pibbles check</c>'s formats.</summary>
internal static class DiagnosticFormatter
{
    private const int TabWidth = 4;

    /// <summary>Camel-case and indented, with backticks and quotes left readable: the output is never embedded in HTML.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Writes a diagnostic for people, as <c>docs/syntax.md</c> shows: a headline, where it is, the source line with the
    /// problem marked and labeled, and the help. Tabs in the line are shown as four spaces.
    /// </summary>
    public static void WritePretty(TextWriter output, SourceText source, Diagnostic diagnostic, bool color)
    {
        SourceLocation location = diagnostic.Location;
        string lineNumber = Number(location.Start.Line);
        string gutter = new(' ', lineNumber.Length);
        TextSpan lineSpan = source.GetLineSpan(location.Start.Line);
        string line = source.Text.Substring(lineSpan.Start, lineSpan.Length);

        int markStart = DisplayWidth(line, location.Start.Column);
        int markEnd = DisplayWidth(line, Math.Min(location.Span.End, lineSpan.End) - lineSpan.Start);
        string marks = new('^', Math.Max(markEnd - markStart, 1));
        string severity = Name(diagnostic.Severity);
        string tint = Tint(diagnostic.Severity);

        output.WriteLine($"{Paint($"{severity}[{diagnostic.Code}]", $"1;{tint}", color)}{Paint($": {diagnostic.Message}", "1", color)}");
        output.WriteLine($"{gutter}{Paint("-->", Blue, color)} {location.Path}:{lineNumber}:{Number(location.Start.Column)}");
        output.WriteLine($"{gutter} {Paint("|", Blue, color)}");
        output.WriteLine($"{Paint($"{lineNumber} |", Blue, color)} {line.Replace("\t", new string(' ', TabWidth), StringComparison.Ordinal)}");
        output.WriteLine($"{gutter} {Paint("|", Blue, color)} {new string(' ', markStart)}{Paint(diagnostic.Label is null ? marks : $"{marks} {diagnostic.Label}", $"1;{tint}", color)}");

        if (diagnostic.Help is not null)
        {
            output.WriteLine($"{gutter} {Paint("|", Blue, color)}");
            output.WriteLine($"{gutter} {Paint("=", Blue, color)} {Paint("help", "1", color)}: {diagnostic.Help}");
        }
    }

    /// <summary>The closing line of the readable format, such as <c>Checked 3 files: 2 errors and 1 warning.</c></summary>
    public static string Summary(int files, IReadOnlyCollection<Diagnostic> diagnostics)
    {
        string[] counts =
        [
            .. new[] { (DiagnosticSeverity.Error, "error"), (DiagnosticSeverity.Warning, "warning"), (DiagnosticSeverity.Info, "note"), (DiagnosticSeverity.Hint, "hint") }
                .Select(entry => (Count: diagnostics.Count(diagnostic => diagnostic.Severity == entry.Item1), Word: entry.Item2))
                .Where(entry => entry.Count > 0)
                .Select(entry => Plural(entry.Count, entry.Word)),
        ];

        string found = counts.Length switch
        {
            0 => "no problems",
            1 => counts[0],
            _ => $"{string.Join(", ", counts[..^1])} and {counts[^1]}",
        };

        return $"Checked {Plural(files, "file")}: {found}.";
    }

    /// <summary>One line in the MSBuild format: <c>file(line,col): severity CODE: message</c>.</summary>
    public static string MSBuild(Diagnostic diagnostic) =>
        $"{diagnostic.Location.Path}({Number(diagnostic.Location.Start.Line)},{Number(diagnostic.Location.Start.Column)}): {Name(diagnostic.Severity)} {diagnostic.Code}: {diagnostic.Message}";

    /// <summary>A JSON array with one object per diagnostic. Lines and columns are 1-based.</summary>
    public static string Json(IEnumerable<Diagnostic> diagnostics) =>
        JsonSerializer.Serialize(diagnostics.Select(diagnostic => new JsonDiagnostic(
            diagnostic.Location.Path,
            diagnostic.Location.Start.Line + 1,
            diagnostic.Location.Start.Column + 1,
            diagnostic.Location.End.Line + 1,
            diagnostic.Location.End.Column + 1,
            Name(diagnostic.Severity),
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Label,
            diagnostic.Help)), JsonOptions);

    private sealed record JsonDiagnostic(string Path, int Line, int Column, int EndLine, int EndColumn, string Severity, string Code, string Message, string? Label, string? Help);

    private const string Blue = "1;34";

    private static string Name(DiagnosticSeverity severity) => severity.ToString().ToLowerInvariant();

    private static string Tint(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "31",
        DiagnosticSeverity.Warning => "33",
        DiagnosticSeverity.Info => "36",
        _ => "37",
    };

    private static string Paint(string text, string codes, bool color) => color ? $"\e[{codes}m{text}\e[0m" : text;

    private static string Number(int zeroBased) => (zeroBased + 1).ToString(CultureInfo.InvariantCulture);

    private static string Plural(int count, string word) => $"{count} {word}{(count == 1 ? "" : "s")}";

    /// <summary>How wide the first <paramref name="column"/> characters of a line are on screen, with tabs four wide.</summary>
    private static int DisplayWidth(string line, int column) =>
        line.Take(column).Sum(c => c is '\t' ? TabWidth : 1) + Math.Max(column - line.Length, 0);
}
