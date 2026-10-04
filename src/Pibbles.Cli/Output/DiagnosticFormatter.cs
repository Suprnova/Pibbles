using System.Globalization;
using Pibbles.Diagnostics;
using Pibbles.Syntax;
using static Pibbles.Cli.Output.Ansi;

namespace Pibbles.Cli.Output;

/// <summary>Writes diagnostics in each of <c>pibbles check</c>'s formats.</summary>
internal static class DiagnosticFormatter
{
    private const int TabWidth = 4;

    /// <summary>
    /// Writes a diagnostic for people, as <c>docs/syntax.md</c> shows: a headline, where it is, the source line with the
    /// problem marked and labeled, and the help. A help's later lines are examples, lined up under its first. With color,
    /// the problem is highlighted in the source line too, quoted code in the prose is cyan, and the help is green. Tabs in
    /// the line are shown as four spaces.
    /// </summary>
    public static void WritePretty(TextWriter output, SourceText source, Diagnostic diagnostic, bool color)
    {
        SourceLocation location = diagnostic.Location;
        string lineNumber = Number(location.Start.Line);
        string gutter = new(' ', lineNumber.Length);
        TextSpan lineSpan = source.GetLineSpan(location.Start.Line);
        string line = source.Text.Substring(lineSpan.Start, lineSpan.Length);

        int markFrom = Math.Min(location.Start.Column, line.Length);
        int markTo = Math.Clamp(location.Span.End - lineSpan.Start, markFrom, line.Length);
        int markStart = DisplayWidth(line, location.Start.Column);
        string marks = new('^', Math.Max(DisplayWidth(line, markTo) - markStart, 1));
        string tint = Tint(diagnostic.Severity);
        string bar = Paint("|", Blue, color);
        string label = diagnostic.Label is null ? "" : $" {Markup.Render(diagnostic.Label, color)}";

        output.WriteLine($"{Paint($"{Name(diagnostic.Severity)}[{diagnostic.Code}]", tint, color)}{Paint($": {Markup.Render(diagnostic.Message, color)}", Bold, color)}");
        output.WriteLine($"{gutter}{Paint("-->", Blue, color)} {location.Path}:{lineNumber}:{Number(location.Start.Column)}");
        output.WriteLine($"{gutter} {bar}");
        output.WriteLine($"{Paint($"{lineNumber} |", Blue, color)} {Expand(line[..markFrom])}{Paint(Expand(line[markFrom..markTo]), tint, color)}{Expand(line[markTo..])}");
        output.WriteLine($"{gutter} {bar} {new string(' ', markStart)}{Paint(marks + label, tint, color)}");

        if (diagnostic.Help is not null)
        {
            string[] help = diagnostic.Help.Split('\n');
            output.WriteLine($"{gutter} {bar}");
            output.WriteLine($"{gutter} {Paint("=", Blue, color)} {Paint("help", Green, color)}: {Markup.Render(help[0], color)}");
            foreach (string example in help[1..])
                output.WriteLine($"{gutter}{HelpIndent}{Paint(example, Example, color)}");
        }
    }

    /// <summary>
    /// The closing line of the readable format, such as <c>Checked 3 files: 2 errors and 1 warning.</c> With color, each
    /// count is in its severity's color, and "no problems" is green.
    /// </summary>
    public static string Summary(int files, IReadOnlyCollection<Diagnostic> diagnostics, bool color = false)
    {
        string[] counts =
        [
            .. new[] { (DiagnosticSeverity.Error, "error"), (DiagnosticSeverity.Warning, "warning"), (DiagnosticSeverity.Info, "note"), (DiagnosticSeverity.Hint, "hint") }
                .Select(entry => (Severity: entry.Item1, Count: diagnostics.Count(diagnostic => diagnostic.Severity == entry.Item1), Word: entry.Item2))
                .Where(entry => entry.Count > 0)
                .Select(entry => Paint(Plural(entry.Count, entry.Word), Tint(entry.Severity), color)),
        ];

        string found = counts.Length switch
        {
            0 => Paint("no problems", Green, color),
            1 => counts[0],
            _ => $"{string.Join(", ", counts[..^1])} and {counts[^1]}",
        };

        return $"Checked {Plural(files, "file")}: {found}.";
    }

    /// <summary>One line in the MSBuild format: <c>file(line,col): severity CODE: message</c>.</summary>
    public static string MSBuild(Diagnostic diagnostic) =>
        $"{diagnostic.Location.Path}({Number(diagnostic.Location.Start.Line)},{Number(diagnostic.Location.Start.Column)}): {Name(diagnostic.Severity)} {diagnostic.Code}: {diagnostic.Message}";

    /// <summary>A JSON array with one object per diagnostic. Lines and columns are 1-based.</summary>
    public static string Json(IEnumerable<Diagnostic> diagnostics) => JsonText.Write(writer =>
    {
        writer.WriteStartArray();
        foreach (Diagnostic diagnostic in diagnostics)
        {
            SourceLocation location = diagnostic.Location;
            writer.WriteStartObject();
            writer.WriteString("path", location.Path);
            writer.WriteNumber("line", location.Start.Line + 1);
            writer.WriteNumber("column", location.Start.Column + 1);
            writer.WriteNumber("endLine", location.End.Line + 1);
            writer.WriteNumber("endColumn", location.End.Column + 1);
            writer.WriteString("severity", Name(diagnostic.Severity));
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("message", diagnostic.Message);
            writer.WriteString("label", diagnostic.Label);
            writer.WriteString("help", diagnostic.Help);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    /// <summary>Lines up a help's later lines with its first, after <c> = help: </c>.</summary>
    private const string HelpIndent = "         ";

    private static string Name(DiagnosticSeverity severity) => severity.ToString().ToLowerInvariant();

    private static string Expand(string text) => text.Replace("\t", new string(' ', TabWidth), StringComparison.Ordinal);

    private static string Number(int zeroBased) => (zeroBased + 1).ToString(CultureInfo.InvariantCulture);

    private static string Plural(int count, string word) => $"{count} {word}{(count == 1 ? "" : "s")}";

    /// <summary>How wide the first <paramref name="column"/> characters of a line are on screen, with tabs four wide.</summary>
    private static int DisplayWidth(string line, int column) =>
        line.Take(column).Sum(c => c is '\t' ? TabWidth : 1) + Math.Max(column - line.Length, 0);
}
