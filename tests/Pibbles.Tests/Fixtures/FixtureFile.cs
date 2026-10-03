using System.Text;
using System.Text.RegularExpressions;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Fixtures;

/// <summary>
/// Diagnostic fixtures: <c>.pib</c> files that mark each expected diagnostic on the line below it,
/// as <c>docs/architecture.md</c> describes.
/// </summary>
/// <remarks>
/// A marker is <c>// ^^^ PIB1001</c>, with the carets under the marked text, or <c>// PIB1001</c> when the
/// text starts in column 0 or 1, where the <c>//</c> sits. A fixture passes when <see cref="Annotate"/>,
/// given the diagnostics the fixture actually produces, gives back the fixture's own text.
/// </remarks>
internal static partial class FixtureFile
{
    private const int MarkerIndent = 2;

    public static string Directory { get; } = Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Fixtures");

    public static IEnumerable<string> FindAll() => System.IO.Directory.EnumerateFiles(Directory, "*.pib", SearchOption.AllDirectories);

    public static IEnumerable<string> MarkedCodes(string text) => Marker().Matches(text).Select(match => match.Groups["code"].Value);

    /// <summary>Rewrites a fixture's markers to show the given diagnostics, with line breaks normalized to <c>\n</c>.</summary>
    public static string Annotate(SourceText source, IEnumerable<Diagnostic> diagnostics)
    {
        ILookup<int, Diagnostic> byLine = diagnostics.ToLookup(diagnostic => diagnostic.Location.Start.Line);
        List<string> lines = [];

        for (int line = 0; line < source.LineCount; line++)
        {
            TextSpan lineSpan = source.GetLineSpan(line);
            string text = source.Text.Substring(lineSpan.Start, lineSpan.Length);

            if (!Marker().IsMatch(text))
                lines.Add(text);

            lines.AddRange(byLine[line]
                .OrderBy(diagnostic => diagnostic.Location.Start.Column)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .Select(diagnostic => MarkerFor(diagnostic, lineSpan)));
        }

        return string.Join('\n', lines);
    }

    private static string MarkerFor(Diagnostic diagnostic, TextSpan lineSpan)
    {
        int column = diagnostic.Location.Start.Column;
        if (column < MarkerIndent)
            return $"// {diagnostic.Code}";

        int lengthOnLine = Math.Min(diagnostic.Location.Span.Length, lineSpan.Length - column);
        return new StringBuilder("//")
            .Append(' ', column - MarkerIndent)
            .Append('^', Math.Max(lengthOnLine, 1))
            .Append(' ')
            .Append(diagnostic.Code)
            .ToString();
    }

    [GeneratedRegex(@"^//(\s*\^+)?\s+(?<code>PIB\d{4})\s*$", RegexOptions.Multiline)]
    private static partial Regex Marker();
}
