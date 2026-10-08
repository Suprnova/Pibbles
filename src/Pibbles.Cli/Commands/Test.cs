using Pibbles.Cli.Output;
using Pibbles.Cli.Projects;
using Pibbles.Cli.Transcripts;
using Pibbles.Compiler;
using Pibbles.Semantics;

namespace Pibbles.Cli.Commands;

/// <summary>
/// <c>pibbles test</c>: replays every transcript under the project's <c>transcripts</c> folder against the story, shows
/// where each one that no longer matches first differs, and, with <c>--update</c>, rewrites those files.
/// </summary>
internal static class Test
{
    /// <summary>Every transcript matched, or was updated.</summary>
    public const int Passed = 0;

    /// <summary>A transcript didn't match, its script failed, or the story has errors.</summary>
    public const int Failed = 1;

    /// <summary>There's nothing to run: no story, no <c>transcripts</c> folder, or no transcripts in it.</summary>
    public const int CouldNotRun = 2;

    /// <summary>How many lines of each side a difference shows.</summary>
    private const int ShownLines = 5;

    /// <summary>Replays the project's transcripts.</summary>
    /// <param name="root">The project's folder, or a folder inside it.</param>
    /// <param name="currentDirectory">The folder paths are shown relative to.</param>
    /// <param name="update">Whether to rewrite each transcript that doesn't match with what the story prints now.</param>
    /// <param name="output">Where the differences and the summary go.</param>
    /// <param name="error">Where problems that stop the run go.</param>
    /// <param name="color">Whether to color the differences.</param>
    public static int Run(string root, string currentDirectory, bool update, TextWriter output, TextWriter error, bool color)
    {
        if (StoryFolder.Load(root, currentDirectory, error) is not { } sources)
            return CouldNotRun;

        CompilationOptions settings = EditorConfigSettings.Load(sources, currentDirectory, error);
        if (Play.Compile(sources, settings, error, color) is not { } story)
            return Failed;

        string folder = Path.Combine(StoryFolder.FindProject(Path.GetFullPath(root, currentDirectory)), "transcripts");
        string[] files = Directory.Exists(folder) ? [.. TranscriptFile.Find(folder)] : [];
        if (files.Length == 0)
        {
            error.WriteLine(Directory.Exists(folder)
                ? $"There are no `.transcript` files in {DisplayPath.Folder(folder, currentDirectory)}."
                : $"I can't find a `transcripts` folder in {DisplayPath.Folder(Path.GetDirectoryName(folder)!, currentDirectory)}. Record one with `pibbles play --record`.");
            return CouldNotRun;
        }

        int passed = 0, updated = 0, failed = 0;
        foreach (string file in files)
        {
            string shown = DisplayPath.Of(file, currentDirectory);
            string text = File.ReadAllText(file);
            string expected = TranscriptFile.Normalize(text);
            string actual;
            try
            {
                actual = TranscriptPlayer.Play(story, expected);
            }
            catch (TranscriptException exception)
            {
                output.WriteLine($"{Ansi.Paint("FAILED", Ansi.Tint(Diagnostics.DiagnosticSeverity.Error), color)} {shown}: {exception.Message}");
                failed++;
                continue;
            }

            if (actual == expected)
            {
                passed++;
            }
            else if (update)
            {
                File.WriteAllText(file, text.Contains("\r\n", StringComparison.Ordinal) ? actual.Replace("\n", "\r\n", StringComparison.Ordinal) : actual);
                output.WriteLine($"{Ansi.Paint("UPDATED", Ansi.Bold, color)} {shown}");
                updated++;
            }
            else
            {
                WriteDifference(output, shown, expected, actual, color);
                failed++;
            }
        }

        output.WriteLine($"Ran {files.Length} transcript{(files.Length == 1 ? "" : "s")}: {string.Join(", ", Counts(passed, updated, failed))}.");
        return failed > 0 ? Failed : Passed;
    }

    private static IEnumerable<string> Counts(int passed, int updated, int failed)
    {
        yield return $"{passed} passed";
        if (updated > 0)
            yield return $"{updated} updated";

        if (failed > 0 || updated == 0)
            yield return $"{failed} failed";
    }

    /// <summary>Shows where a transcript first differs: a few lines of what the file says, then what the story prints now, from that line on.</summary>
    private static void WriteDifference(TextWriter output, string file, string expected, string actual, bool color)
    {
        string[] want = expected.Split('\n');
        string[] got = actual.Split('\n');
        int first = 0;
        while (first < want.Length && first < got.Length && want[first] == got[first])
            first++;

        output.WriteLine($"{Ansi.Paint("FAILED", Ansi.Tint(Diagnostics.DiagnosticSeverity.Error), color)} {file} differs from line {first + 1}:");
        foreach (string line in Window(want, first))
            output.WriteLine(Ansi.Paint($"  - {line}", "31", color));

        foreach (string line in Window(got, first))
            output.WriteLine(Ansi.Paint($"  + {line}", "32", color));
    }

    /// <summary>Up to <see cref="ShownLines"/> lines from <paramref name="first"/>, or a note that the text ends there.</summary>
    private static IEnumerable<string> Window(string[] lines, int first) =>
        first < lines.Length ? lines.Skip(first).Take(ShownLines) : ["(the text ends here)"];
}
