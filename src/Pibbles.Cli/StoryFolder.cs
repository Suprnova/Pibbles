using System.Text.Json;
using Pibbles.Syntax;

namespace Pibbles.Cli;

/// <summary>
/// Finds a project's story on disk: the folder named by <c>pibbles.json</c>'s <c>story</c> setting, or <c>story</c>
/// without one, and every <c>.pib</c> file under it.
/// </summary>
internal static class StoryFolder
{
    private const string DefaultStory = "story";

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

        if (!TryReadStorySetting(fullRoot, error, out string story))
            return null;

        string storyFolder = Path.GetFullPath(story, fullRoot);
        if (!Directory.Exists(storyFolder))
        {
            error.WriteLine($"I can't find the story folder `{Display(storyFolder, currentDirectory)}`. Put the story's `.pib` files there, or name another folder with `story` in `pibbles.json`.");
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

    private static bool TryReadStorySetting(string root, TextWriter error, out string story)
    {
        story = DefaultStory;
        string settings = Path.Combine(root, "pibbles.json");
        if (!File.Exists(settings))
            return true;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settings));
            if (!document.RootElement.TryGetProperty("story", out JsonElement value))
                return true;

            if (value.ValueKind is JsonValueKind.String && value.GetString() is { Length: > 0 } folder)
            {
                story = folder;
                return true;
            }

            error.WriteLine("`story` in `pibbles.json` has to be a folder name, in quotes.");
            return false;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"I can't read `pibbles.json`: {exception.Message}");
            return false;
        }
    }

    private static string Display(string path, string currentDirectory) =>
        Path.GetRelativePath(currentDirectory, path).Replace('\\', '/');
}
