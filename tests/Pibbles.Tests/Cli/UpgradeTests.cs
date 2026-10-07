using Pibbles.Cli.Commands;
using Pibbles.Cli.Projects;

namespace Pibbles.Tests.Cli;

public sealed class UpgradeTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("pibbles-");
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        root.Delete(recursive: true);
    }

    private string SettingsFile => Path.Combine(root.FullName, "pibbles.json");

    [Fact]
    public void Run_OlderSchema_RewritesInCurrentSchemaKeepingSettings()
    {
        File.WriteAllText(SettingsFile, """{ "schema": 1, "story": "dialogue" }""");

        int exitCode = Upgrade.Run(".", root.FullName, output, error);

        Assert.Equal((Check.Passed, "Upgraded `pibbles.json` from schema 1 to schema 2.\n"), (exitCode, output.ToString().ReplaceLineEndings("\n")));
        Assert.Equal(new ProjectSettings(["dialogue"]).ToJson(), File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void Run_FromInsideProject_FindsItsSettings()
    {
        File.WriteAllText(SettingsFile, """{ "schema": 1 }""");
        Directory.CreateDirectory(Path.Combine(root.FullName, "story", "rooms"));

        int exitCode = Upgrade.Run(".", Path.Combine(root.FullName, "story", "rooms"), output, error);

        Assert.Equal((Check.Passed, "Upgraded `../../pibbles.json` from schema 1 to schema 2.\n"), (exitCode, output.ToString().ReplaceLineEndings("\n")));
    }

    [Theory]
    [InlineData("""{ "schema": 2, "storyFolders": ["story"] }""")]
    [InlineData("{}")]
    public void Run_CurrentSchema_LeavesFileAlone(string json)
    {
        File.WriteAllText(SettingsFile, json);

        int exitCode = Upgrade.Run(".", root.FullName, output, error);

        Assert.Equal((Check.Passed, "`pibbles.json` already uses schema 2, the newest.\n"), (exitCode, output.ToString().ReplaceLineEndings("\n")));
        Assert.Equal(json, File.ReadAllText(SettingsFile));
    }

    [Theory]
    [InlineData(null, "There's no `pibbles.json` in this folder or the folders above it.")]
    [InlineData("""{ "schema": 1, "story": 3 }""", "`story` in `pibbles.json` has to be a folder name")]
    public void Run_NothingToUpgrade_ReportsWhy(string? json, string message)
    {
        if (json is not null)
            File.WriteAllText(SettingsFile, json);

        int exitCode = Upgrade.Run(".", root.FullName, output, error);

        Assert.Equal(Check.CouldNotRun, exitCode);
        Assert.StartsWith(message, error.ToString());
    }
}
