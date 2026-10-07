using Pibbles.Cli.Output;
using Pibbles.Cli.Projects;

namespace Pibbles.Cli.Commands;

/// <summary>
/// <c>pibbles upgrade [root]</c>: rewrites the project's <c>pibbles.json</c> in the current schema, keeping its
/// settings. The other commands read an older schema as it is, and only note that this command updates it.
/// </summary>
internal static class Upgrade
{
    /// <summary>Finds the project the way <c>check</c> does, and rewrites its <c>pibbles.json</c> if it's in an older schema.</summary>
    /// <returns><see cref="Check.Passed"/>, or <see cref="Check.CouldNotRun"/> if there's no settings file to read.</returns>
    public static int Run(string root, string currentDirectory, TextWriter output, TextWriter error)
    {
        string start = Path.GetFullPath(root, currentDirectory);
        if (!Directory.Exists(start))
        {
            error.WriteLine($"I can't find the folder `{root}`.");
            return Check.CouldNotRun;
        }

        string file = Path.Combine(StoryFolder.FindProject(start), ProjectSettings.FileName);
        if (!File.Exists(file))
        {
            error.WriteLine($"There's no `{ProjectSettings.FileName}` in {DisplayPath.Folder(start, currentDirectory)} or the folders above it. Run `pibbles init` to start a project.");
            return Check.CouldNotRun;
        }

        if (ProjectSettings.Parse(File.ReadAllText(file), error) is not { } settings)
            return Check.CouldNotRun;

        string shown = DisplayPath.Of(file, currentDirectory);
        if (settings.FileSchema == ProjectSettings.Schema)
        {
            output.WriteLine($"`{shown}` already uses schema {ProjectSettings.Schema}, the newest.");
            return Check.Passed;
        }

        File.WriteAllText(file, settings.ToJson());
        output.WriteLine($"Upgraded `{shown}` from schema {settings.FileSchema} to schema {ProjectSettings.Schema}.");
        return Check.Passed;
    }
}
