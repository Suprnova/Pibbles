using System.Reflection;

namespace Pibbles.Cli;

/// <summary>
/// <c>pibbles init [folder]</c>: starts a project with a <c>pibbles.json</c> and a story folder. By default the folder
/// gets a short example story whose comments explain each part, which <c>pibbles check</c> passes.
/// </summary>
internal static class Init
{
    private static readonly (string File, string Description)[] Starter =
    [
        ("cast.pib", "the cast list, with one character"),
        ("start.pib", "a first conversation"),
    ];

    /// <summary>
    /// Creates the project in <paramref name="folder"/>, which may not exist yet. An existing <c>pibbles.json</c> is
    /// never overwritten, and a story folder that already has <c>.pib</c> files is left as it is.
    /// </summary>
    public static int Run(string folder, string currentDirectory, bool blank, TextWriter output, TextWriter error)
    {
        string root = Path.GetFullPath(folder, currentDirectory);
        string settingsFile = Path.Combine(root, ProjectSettings.FileName);
        if (File.Exists(settingsFile))
        {
            error.WriteLine($"`{DisplayPath.Of(settingsFile, currentDirectory)}` already exists, so this is already a Pibbles project. I left it as it is.");
            return Check.CouldNotRun;
        }

        ProjectSettings settings = ProjectSettings.Default;
        string story = Path.Combine(root, settings.Story);
        bool hasSources = Directory.Exists(story) && Directory.EnumerateFiles(story, "*.pib", SearchOption.AllDirectories).Any();
        List<(string Path, string Description)> created = [(settingsFile, "the project's settings")];

        if (!Directory.Exists(story) && blank)
            created.Add((story + "/", "the story folder, for your .pib files"));

        Directory.CreateDirectory(story);
        File.WriteAllText(settingsFile, settings.ToJson());

        if (!blank && !hasSources)
        {
            foreach ((string file, string description) in Starter)
            {
                string path = Path.Combine(story, file);
                File.WriteAllText(path, ReadStarter(file));
                created.Add((path, description));
            }
        }

        string[] paths = [.. created.Select(entry => DisplayPath.Of(entry.Path, currentDirectory))];
        int width = paths.Max(path => path.Length);
        output.WriteLine($"I made a Pibbles project in {DisplayPath.Folder(root, currentDirectory)}:");
        foreach ((string path, (_, string description)) in paths.Zip(created))
            output.WriteLine($"  {path.PadRight(width)}  {description}");

        if (hasSources)
            output.WriteLine($"{DisplayPath.Folder(story, currentDirectory)} already has .pib files, so I left them as they are.");

        output.WriteLine();
        string check = DisplayPath.Of(root, currentDirectory) is "." ? "pibbles check" : $"pibbles check {DisplayPath.Of(root, currentDirectory)}";
        output.WriteLine(blank && !hasSources
            ? $"Next, add .pib files to {DisplayPath.Folder(story, currentDirectory)}, then run `{check}` to check them."
            : $"Next, run `{check}` to check the story.");

        return Check.Passed;
    }

    private static string ReadStarter(string file)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Starter/{file}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
