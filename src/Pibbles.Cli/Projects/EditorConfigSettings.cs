using EditorConfig.Core;
using Pibbles.Configuration;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Cli.Projects;

/// <summary>
/// Reads each source file's <c>.editorconfig</c> settings the way editors do: from the files in its folder and every
/// folder above it, up to one with <c>root = true</c>, nearer files overriding farther ones.
/// </summary>
internal static class EditorConfigSettings
{
    /// <summary>Resolves the settings of every source, writing each problem with a Pibbles setting to <paramref name="error"/> once.</summary>
    /// <param name="sources">The story's sources, whose paths are relative to <paramref name="currentDirectory"/>.</param>
    /// <param name="currentDirectory">The folder the sources' paths are relative to.</param>
    /// <param name="error">Where problems with Pibbles' settings are written.</param>
    public static CompilationOptions Load(IReadOnlyList<SourceText> sources, string currentDirectory, TextWriter error)
    {
        var parser = new EditorConfigParser();
        Dictionary<string, FileSettings> settings = [];
        HashSet<string> reported = [];

        foreach (SourceText source in sources)
        {
            FileConfiguration configuration = parser.Parse(Path.GetFullPath(source.Path, currentDirectory));
            FileSettings file = FileSettings.From(configuration.Properties);
            settings[source.Path] = file;

            foreach (string problem in file.Problems.Where(reported.Add))
                error.WriteLine($"In the .editorconfig settings for {source.Path}: {problem}");
        }

        return new(settings);
    }
}
