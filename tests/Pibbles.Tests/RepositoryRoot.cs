namespace Pibbles.Tests;

/// <summary>Locates the repository checkout the tests run from, for tests that read committed files.</summary>
internal static class RepositoryRoot
{
    /// <summary>The folder that contains <c>Pibbles.slnx</c>.</summary>
    public static string Path { get; } = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Pibbles.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException($"No Pibbles.slnx above {AppContext.BaseDirectory}.");
    }
}
