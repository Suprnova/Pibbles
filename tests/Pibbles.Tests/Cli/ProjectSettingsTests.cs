using Pibbles.Cli.Projects;

namespace Pibbles.Tests.Cli;

public sealed class ProjectSettingsTests : IDisposable
{
    private readonly StringWriter error = new();

    public void Dispose() => error.Dispose();

    [Theory]
    [InlineData("""{ "schema": 2, "storyFolders": ["dialogue"] }""", new[] { "dialogue" })]
    [InlineData("""{ "storyFolders": ["scripts/dialogue", "chapters"] }""", new[] { "scripts/dialogue", "chapters" })]
    [InlineData("""{ "schema": 1, "story": "dialogue" }""", new[] { "dialogue" })]
    [InlineData("""{ "schema": 1 }""", new[] { "story" })]
    [InlineData("{}", new[] { "story" })]
    public void Parse_ValidSettings_FillsDefaultsForMissingKeys(string json, string[] storyFolders)
    {
        ProjectSettings? settings = ProjectSettings.Parse(json, error);

        Assert.Equal(storyFolders, settings!.StoryFolders);
    }

    [Theory]
    [InlineData("{ not json", "I can't read `pibbles.json`:")]
    [InlineData("[1, 2]", "`pibbles.json` has to hold its settings in braces")]
    [InlineData("""{ "schema": "one" }""", "`schema` in `pibbles.json` has to be a whole number")]
    [InlineData("""{ "schema": 0 }""", "`schema` in `pibbles.json` has to be a whole number")]
    [InlineData("""{ "schema": 99 }""", "This project's `pibbles.json` was written by a newer version of Pibbles (schema 99; this version reads up to 2).")]
    [InlineData("""{ "storyFolders": [] }""", "`storyFolders` in `pibbles.json` has to be a list of folder names")]
    [InlineData("""{ "storyFolders": "story" }""", "`storyFolders` in `pibbles.json` has to be a list of folder names")]
    [InlineData("""{ "storyFolders": ["story", ""] }""", "`storyFolders` in `pibbles.json` has to be a list of folder names")]
    [InlineData("""{ "schema": 1, "storyFolders": ["story"] }""", "`storyFolders` in `pibbles.json` needs schema 2 or later.")]
    [InlineData("""{ "schema": 1, "story": "" }""", "`story` in `pibbles.json` has to be a folder name")]
    [InlineData("""{ "story": "story" }""", "`story` in `pibbles.json` is `storyFolders` from schema 2 on")]
    [InlineData("""{ "stroy": "dialogue" }""", "I don't know the setting `stroy` in `pibbles.json`. The settings are `schema` and `storyFolders`.")]
    public void Parse_InvalidSettings_ReportsWhyAndReturnsNull(string json, string message)
    {
        ProjectSettings? settings = ProjectSettings.Parse(json, error);

        Assert.Null(settings);
        Assert.StartsWith(message, error.ToString());
    }

    [Fact]
    public void ToJson_Default_WritesCurrentSchemaThatParsesBack()
    {
        string json = ProjectSettings.Default.ToJson();

        Assert.Equal("{\n  \"schema\": 2,\n  \"storyFolders\": [\n    \"story\"\n  ]\n}\n", json.ReplaceLineEndings("\n"));
        Assert.Equal(ProjectSettings.Default.StoryFolders, ProjectSettings.Parse(json, error)!.StoryFolders);
    }
}
