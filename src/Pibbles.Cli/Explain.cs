using System.Reflection;

namespace Pibbles.Cli;

/// <summary>
/// <c>pibbles explain &lt;code&gt;</c>: prints a diagnostic's entry from <c>docs/diagnostics.md</c>, which the CLI
/// embeds when it's built, so the explanation and the docs never drift apart.
/// </summary>
internal static class Explain
{
    public static int Run(string code, TextWriter output, TextWriter error) => Run(code, Catalog.Value, output, error);

    /// <summary>
    /// Prints <paramref name="code"/>'s headline and help from the catalog's table, then its section under Explanations,
    /// with code fences dropped and their contents indented.
    /// </summary>
    public static int Run(string code, string catalog, TextWriter output, TextWriter error)
    {
        string normalized = code.Trim().ToUpperInvariant();
        string[] lines = catalog.ReplaceLineEndings("\n").Split('\n');
        string[]? row = lines
            .Where(line => line.StartsWith($"| {normalized} |", StringComparison.Ordinal))
            .Select(line => line.Split(" | "))
            .FirstOrDefault();

        if (row is not [_, var severity, var message, var help])
        {
            error.WriteLine($"I don't know the code `{code}`. Codes look like `PIB1011`, as in the brackets of `error[PIB1011]`.");
            return Check.CouldNotRun;
        }

        output.WriteLine($"{normalized} ({severity.ToLowerInvariant()}): {message}");
        output.WriteLine($"Help: {help.TrimEnd(' ', '|')}");

        string[] section = [.. lines
            .SkipWhile(line => line != $"### {normalized}")
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("### ", StringComparison.Ordinal))];

        bool inCode = false;
        foreach (string line in section)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
                inCode = !inCode;
            else
                output.WriteLine(inCode ? $"    {line}" : line);
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
