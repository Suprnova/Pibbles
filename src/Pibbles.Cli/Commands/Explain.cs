using System.Reflection;
using Pibbles.Cli.Output;
using Pibbles.Diagnostics;
using static Pibbles.Cli.Output.Ansi;

namespace Pibbles.Cli.Commands;

/// <summary>
/// <c>pibbles explain &lt;code&gt;</c>: prints a diagnostic's entry from <c>docs/diagnostics.md</c>, which the CLI
/// embeds when it's built, so the explanation and the docs never drift apart.
/// </summary>
internal static class Explain
{
    public static int Run(string code, TextWriter output, TextWriter error, bool color = false) => Run(code, Catalog.Value, output, error, color);

    /// <summary>
    /// Prints <paramref name="code"/>'s headline and help from the catalog's table, laid out like <c>pibbles check</c>
    /// prints them, then its section under Explanations: prose with its markup rendered, and examples indented, in
    /// green with color.
    /// </summary>
    public static int Run(string code, string catalog, TextWriter output, TextWriter error, bool color = false)
    {
        string normalized = code.Trim().ToUpperInvariant();
        string[] lines = catalog.ReplaceLineEndings("\n").Split('\n');
        string[]? row = lines
            .Where(line => line.StartsWith($"| {normalized} |", StringComparison.Ordinal))
            .Select(line => line.Split(" | "))
            .FirstOrDefault();

        if (row is not [_, var severity, var message, var help] || !Enum.TryParse(severity, out DiagnosticSeverity level))
        {
            error.WriteLine($"I don't know the code `{code}`. Codes look like `PIB1011`, as in the brackets of `error[PIB1011]`.");
            return Check.CouldNotRun;
        }

        output.WriteLine($"{Paint($"{severity.ToLowerInvariant()}[{normalized}]", Tint(level), color)}{Paint($": {Markup.Render(message, color)}", Bold, color)}");
        if (help.TrimEnd(' ', '|') is var fix and not "—")
            output.WriteLine($"  {Paint("=", Blue, color)} {Paint("help", Green, color)}: {Markup.Render(fix, color)}");

        string[] section = [.. lines
            .SkipWhile(line => line != $"### {normalized}")
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("### ", StringComparison.Ordinal))];

        bool inExample = false;
        foreach (string line in section)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
                inExample = !inExample;
            else
                output.WriteLine(inExample ? $"    {Paint(line, Example, color)}" : Markup.Render(line, color));
        }

        return Check.Passed;
    }

    private static readonly Lazy<string> Catalog = new(() =>
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("diagnostics.md")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
