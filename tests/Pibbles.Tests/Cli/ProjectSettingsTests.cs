using Pibbles.Cli;

namespace Pibbles.Tests.Cli;

public sealed class ProjectSettingsTests : IDisposable
{
    private readonly StringWriter error = new();

    public void Dispose() => error.Dispose();

    [Theory]
    [InlineData("""{ "schema": 1, "story": "dialogue" }""", "dialogue")]
    [InlineData("""{ "story": "scripts/dialogue" }""", "scripts/dialogue")]
    [InlineData("""{ "schema": 1 }""", "story")]
    [InlineData("{}", "story")]
    public void Parse_ValidSettings_FillsDefaultsForMissingKeys(string json, string story)
    {
        ProjectSettings? settings = ProjectSettings.Parse(json, error);

        Assert.Equal(new ProjectSettings(story), settings);
    }

    [Theory]
    [InlineData("{ not json", "I can't read `pibbles.json`:")]
    [InlineData("[1, 2]", "`pibbles.json` has to hold its settings in braces")]
    [InlineData("""{ "schema": "one" }""", "`schema` in `pibbles.json` has to be a whole number")]
    [InlineData("""{ "schema": 0 }""", "`schema` in `pibbles.json` has to be a whole number")]
    [InlineData("""{ "schema": 99 }""", "This project's `pibbles.json` was written by a newer version of Pibbles (schema 99; this version reads up to 1).")]
    [InlineData("""{ "story": "" }""", "`story` in `pibbles.json` has to be a folder name")]
    [InlineData("""{ "stroy": "dialogue" }""", "I don't know the setting `stroy` in `pibbles.json`. The settings are `schema` and `story`.")]
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

        Assert.Equal("{\n  \"schema\": 1,\n  \"story\": \"story\"\n}\n", json.ReplaceLineEndings("\n"));
        Assert.Equal(ProjectSettings.Default, ProjectSettings.Parse(json, error));
    }
}
