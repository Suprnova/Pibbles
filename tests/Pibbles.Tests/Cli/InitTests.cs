using Pibbles.Cli;
using Pibbles.Syntax;

namespace Pibbles.Tests.Cli;

public sealed class InitTests : IDisposable
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

    [Fact]
    public void Run_NewFolder_CreatesSettingsAndStarterStory()
    {
        int exitCode = Init.Run("my-story", root.FullName, blank: false, output, error);

        Assert.Equal(Check.Passed, exitCode);
        Assert.Equal(ProjectSettings.Default.ToJson(), File.ReadAllText(Path.Combine(root.FullName, "my-story", "pibbles.json")));
        Assert.Equal(["cast.pib", "start.pib"], Directory.EnumerateFiles(Path.Combine(root.FullName, "my-story", "story")).Select(Path.GetFileName).Order());
        Assert.EndsWith("Next, run `pibbles check my-story` to check the story.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_StarterStory_PassesCheckWithNoProblems()
    {
        Init.Run(".", root.FullName, blank: false, output, error);
        IReadOnlyList<SourceText> sources = StoryFolder.Load(".", root.FullName, error)!;

        int exitCode = Check.Run(sources, new(OutputFormat.MSBuild), output);

        Assert.Equal(Check.Passed, exitCode);
        Assert.StartsWith("I made a Pibbles project in this folder:", output.ToString());
        Assert.EndsWith("Next, run `pibbles check` to check the story.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_Blank_CreatesOnlySettingsAndEmptyStoryFolder()
    {
        Init.Run(".", root.FullName, blank: true, output, error);

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root.FullName, "story")));
        Assert.Matches(@"story/ +the story folder, for your \.pib files", output.ToString());
    }

    [Fact]
    public void Run_ExistingSettings_LeavesThemAndCouldNotRun()
    {
        string settings = Path.Combine(root.FullName, "pibbles.json");
        File.WriteAllText(settings, """{ "story": "mine" }""");

        int exitCode = Init.Run(".", root.FullName, blank: false, output, error);

        Assert.Equal((Check.CouldNotRun, """{ "story": "mine" }"""), (exitCode, File.ReadAllText(settings)));
        Assert.StartsWith("`pibbles.json` already exists", error.ToString());
    }

    [Fact]
    public void Run_StoryFolderWithSources_AddsOnlySettings()
    {
        Directory.CreateDirectory(Path.Combine(root.FullName, "story"));
        File.WriteAllText(Path.Combine(root.FullName, "story", "mine.pib"), "== mine");

        Init.Run(".", root.FullName, blank: false, output, error);

        Assert.Equal(["mine.pib"], Directory.EnumerateFiles(Path.Combine(root.FullName, "story")).Select(Path.GetFileName));
        Assert.Contains("`story` already has .pib files, so I left them as they are.", output.ToString());
    }
}
