using Pibbles.Syntax;

namespace Pibbles.Cli;

/// <summary>
/// Finds a project's story on disk: the folder named by <c>pibbles.json</c>'s <c>story</c> setting, or <c>story</c>
/// without one, and every <c>.pib</c> file under it.
/// </summary>
internal static class StoryFolder
{
    /// <summary>
    /// Reads every source file in the story under <paramref name="root"/>. Paths are shown relative to
    /// <paramref name="currentDirectory"/>, with <c>/</c> between folders. Returns <see langword="null"/> after writing
    /// the problem to <paramref name="error"/> when there's no story to read.
    /// </summary>
    public static IReadOnlyList<SourceText>? Load(string root, string currentDirectory, TextWriter error)
    {
        string fullRoot = Path.GetFullPath(root, currentDirectory);
        if (!Directory.Exists(fullRoot))
        {
            error.WriteLine($"I can't find the folder `{root}`.");
            return null;
        }

        if (ReadSettings(fullRoot, error) is not { } settings)
            return null;

        string storyFolder = Path.GetFullPath(settings.Story, fullRoot);
        if (!Directory.Exists(storyFolder))
        {
            error.WriteLine($"I can't find the story folder `{Display(storyFolder, currentDirectory)}`. Put the story's `.pib` files there, name another folder with `story` in `pibbles.json`, or run `pibbles init` to start a new project.");
            return null;
        }

        string[] files = [.. Directory.EnumerateFiles(storyFolder, "*.pib", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
        if (files.Length == 0)
        {
            error.WriteLine($"There are no `.pib` files in `{Display(storyFolder, currentDirectory)}`.");
            return null;
        }

        return [.. files.Select(file => new SourceText(Display(file, currentDirectory), File.ReadAllText(file)))];
    }

    /// <summary>Reads the project's settings, or the defaults when it has no <c>pibbles.json</c>.</summary>
    public static ProjectSettings? ReadSettings(string root, TextWriter error)
    {
        string file = Path.Combine(root, ProjectSettings.FileName);
        return File.Exists(file) ? ProjectSettings.Parse(File.ReadAllText(file), error) : ProjectSettings.Default;
    }

    private static string Display(string path, string currentDirectory) =>
        Path.GetRelativePath(currentDirectory, path).Replace('\\', '/');
}
