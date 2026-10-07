using Pibbles.Cli.Output;
using Pibbles.Syntax;

namespace Pibbles.Cli.Projects;

/// <summary>
/// Finds a project's story on disk. The project is the nearest folder with a <c>pibbles.json</c>, from the given folder
/// up, the way git finds a repository, so the tools work from anywhere inside a project. Its story is every <c>.pib</c>
/// file under the folders <c>pibbles.json</c>'s <c>storyFolders</c> setting lists, or under <c>story</c>.
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
        string start = Path.GetFullPath(root, currentDirectory);
        if (!Directory.Exists(start))
        {
            error.WriteLine($"I can't find the folder `{root}`.");
            return null;
        }

        string fullRoot = FindProject(start);

        if (ReadSettings(fullRoot, error) is not { } settings)
            return null;

        string[] storyFolders = [.. settings.StoryFolders.Select(folder => Path.GetFullPath(folder, fullRoot))];
        if (storyFolders.FirstOrDefault(folder => !Directory.Exists(folder)) is { } missing)
        {
            error.WriteLine($"I can't find the story folder `{DisplayPath.Of(missing, currentDirectory)}`. Put the story's `.pib` files there, list the story's folders in `storyFolders` in `pibbles.json`, or run `pibbles init` to start a new project.");
            return null;
        }

        string[] files =
        [
            .. storyFolders
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.pib", SearchOption.AllDirectories))
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];
        if (files.Length == 0)
        {
            error.WriteLine($"There are no `.pib` files in {string.Join(" or ", storyFolders.Select(folder => DisplayPath.Folder(folder, currentDirectory)))}.");
            return null;
        }

        return [.. files.Select(file => new SourceText(DisplayPath.Of(file, currentDirectory), File.ReadAllText(file)))];
    }

    /// <summary>The nearest folder with a <c>pibbles.json</c>, from <paramref name="start"/> up, or <paramref name="start"/> itself if there's none.</summary>
    public static string FindProject(string start)
    {
        for (DirectoryInfo? folder = new(start); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, ProjectSettings.FileName)))
                return folder.FullName;
        }

        return start;
    }

    /// <summary>
    /// Reads the project's settings, or the defaults when it has no <c>pibbles.json</c>. A file in an older schema still
    /// reads, with a note on <paramref name="error"/> that <c>pibbles upgrade</c> updates it.
    /// </summary>
    public static ProjectSettings? ReadSettings(string root, TextWriter error)
    {
        string file = Path.Combine(root, ProjectSettings.FileName);
        ProjectSettings? settings = File.Exists(file) ? ProjectSettings.Parse(File.ReadAllText(file), error) : ProjectSettings.Default;
        if (settings?.FileSchema < ProjectSettings.Schema)
            error.WriteLine($"Note: `{ProjectSettings.FileName}` uses schema {settings.FileSchema}, and this version of Pibbles writes schema {ProjectSettings.Schema}. It still works as it is. Run `pibbles upgrade` to update it.");

        return settings;
    }
}
