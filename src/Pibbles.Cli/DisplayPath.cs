namespace Pibbles.Cli;

/// <summary>How paths read in the CLI's output: relative to the current directory, with <c>/</c> between folders.</summary>
internal static class DisplayPath
{
    public static string Of(string path, string currentDirectory) =>
        Path.GetRelativePath(currentDirectory, path).Replace('\\', '/');

    /// <summary>A folder as a message names it: <c>this folder</c> for the current directory, or its path in backticks.</summary>
    public static string Folder(string path, string currentDirectory) =>
        Of(path, currentDirectory) is "." ? "this folder" : $"`{Of(path, currentDirectory)}`";
}
