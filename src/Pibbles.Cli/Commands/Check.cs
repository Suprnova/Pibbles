using Pibbles.Cli.Output;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Cli.Commands;

/// <summary>How <c>pibbles check</c> prints diagnostics.</summary>
internal enum OutputFormat
{
    /// <summary>For people: each diagnostic with its source line, the problem marked, and a fix.</summary>
    Pretty,

    /// <summary>One line per diagnostic, <c>file(line,col): severity CODE: message</c>, for editors and CI annotations.</summary>
    MSBuild,

    /// <summary>A JSON array, for tools.</summary>
    Json,
}

/// <summary>The settings of one <c>pibbles check</c> run.</summary>
/// <param name="Format">How to print diagnostics.</param>
/// <param name="WarnAsError">Whether warnings fail the check.</param>
/// <param name="Style">Whether to show style hints.</param>
/// <param name="Color">Whether the readable format uses color.</param>
internal sealed record CheckOptions(OutputFormat Format, bool WarnAsError = false, bool Style = false, bool Color = false);

/// <summary><c>pibbles check</c>: parses every source file, prints what it finds, and returns the exit code.</summary>
internal static class Check
{
    /// <summary>Everything was checked, and nothing failed the check.</summary>
    public const int Passed = 0;

    /// <summary>Everything was checked, and an error, or a warning with <c>--warnaserror</c>, failed the check.</summary>
    public const int Failed = 1;

    /// <summary>The check couldn't run, for example because there's no story folder.</summary>
    public const int CouldNotRun = 2;

    public static int Run(IReadOnlyList<SourceText> sources, CheckOptions options, TextWriter output)
    {
        List<(SourceText Source, Diagnostic Diagnostic)> found =
        [
            .. sources.SelectMany(source => SyntaxTree.Parse(source).Diagnostics
                .Where(diagnostic => options.Style || diagnostic.Severity is not DiagnosticSeverity.Hint)
                .OrderBy(diagnostic => diagnostic.Location.Span.Start)
                .Select(diagnostic => (source, diagnostic))),
        ];

        switch (options.Format)
        {
            case OutputFormat.MSBuild:
                foreach ((_, Diagnostic diagnostic) in found)
                    output.WriteLine(DiagnosticFormatter.MSBuild(diagnostic));
                break;

            case OutputFormat.Json:
                output.WriteLine(DiagnosticFormatter.Json(found.Select(entry => entry.Diagnostic)));
                break;

            default:
                foreach ((SourceText source, Diagnostic diagnostic) in found)
                {
                    DiagnosticFormatter.WritePretty(output, source, diagnostic, options.Color);
                    output.WriteLine();
                }

                output.WriteLine(DiagnosticFormatter.Summary(sources.Count, [.. found.Select(entry => entry.Diagnostic)], options.Color));
                break;
        }

        return ExitCode(found.Select(entry => entry.Diagnostic), options.WarnAsError);
    }

    public static int ExitCode(IEnumerable<Diagnostic> diagnostics, bool warnAsError) =>
        diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error || warnAsError && diagnostic.Severity is DiagnosticSeverity.Warning)
            ? Failed
            : Passed;
}
