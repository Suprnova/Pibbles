using Pibbles.Cli.Projects;
using Pibbles.Syntax;

namespace Pibbles.Tests.Cli;

public sealed class StoryFolderTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("pibbles-");
    private readonly StringWriter error = new();

    public void Dispose()
    {
        error.Dispose();
        root.Delete(recursive: true);
    }

    [Fact]
    public void Load_DefaultStoryFolder_ReadsEveryFileInOrder()
    {
        Write("story/rooms/kitchen.pib", "== kitchen.door");
        Write("story/defs.pib", "@var $x = 1");
        Write("notes.txt", "not a source");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(".", root.FullName, error);

        Assert.Equal(["story/defs.pib", "story/rooms/kitchen.pib"], sources!.Select(source => source.Path));
    }

    [Fact]
    public void Load_StoryFoldersSetting_ReadsEachFolderOnce()
    {
        Write("pibbles.json", """{ "schema": 2, "storyFolders": ["dialogue", "chapters", "dialogue/rooms"] }""");
        Write("dialogue/a.pib", "== a");
        Write("dialogue/rooms/b.pib", "== b");
        Write("chapters/c.pib", "== c");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(".", root.FullName, error);

        Assert.Equal(["chapters/c.pib", "dialogue/a.pib", "dialogue/rooms/b.pib"], sources!.Select(source => source.Path));
    }

    [Fact]
    public void Load_SchemaOneStorySetting_ReadsThatFolder()
    {
        Write("pibbles.json", """{ "schema": 1, "story": "dialogue" }""");
        Write("dialogue/a.pib", "== a");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(".", root.FullName, error);

        Assert.Equal(["dialogue/a.pib"], sources!.Select(source => source.Path));
        Assert.Equal("Note: `pibbles.json` uses schema 1, and this version of Pibbles writes schema 2. It still works as it is. Run `pibbles upgrade` to update it.\n", error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Load_CurrentSchema_WritesNoNote()
    {
        Write("pibbles.json", """{ "schema": 2, "storyFolders": ["story"] }""");
        Write("story/a.pib", "== a");

        StoryFolder.Load(".", root.FullName, error);

        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Load_RootBelowCurrentDirectory_ShowsPathsFromCurrentDirectory()
    {
        Write("game/story/a.pib", "== a");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load("game", root.FullName, error);

        Assert.Equal(["game/story/a.pib"], sources!.Select(source => source.Path));
    }

    [Theory]
    [InlineData("story")]
    [InlineData("story/rooms")]
    public void Load_FromInsideProject_FindsProjectAbove(string folder)
    {
        Write("pibbles.json", "{ \"schema\": 1 }");
        Write("story/start.pib", "== start");
        Write("story/rooms/kitchen.pib", "== kitchen");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(".", Path.Combine(root.FullName, folder), error);

        Assert.Equal(2, sources!.Count);
        Assert.Contains(sources, source => source.Path.EndsWith("kitchen.pib", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_FromInsideStoryFolder_ShowsPathsFromThere()
    {
        Write("pibbles.json", "{}");
        Write("story/start.pib", "== start");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(".", Path.Combine(root.FullName, "story"), error);

        Assert.Equal(["start.pib"], sources!.Select(source => source.Path));
    }

    [Theory]
    [InlineData(null, null, "I can't find the folder `missing`.", "missing")]
    [InlineData(null, null, "I can't find the story folder `story`.", ".")]
    [InlineData("pibbles.json", "{ not json", "I can't read `pibbles.json`:", ".")]
    [InlineData("pibbles.json", """{ "storyFolders": [3] }""", "`storyFolders` in `pibbles.json` has to be a list of folder names", ".")]
    [InlineData("story/readme.txt", "no sources", "There are no `.pib` files in `story`.", ".")]
    [InlineData("pibbles.json", """{ "storyFolders": [".", "extra"] }""", "I can't find the story folder `extra`.", ".")]
    public void Load_NoStoryToRead_ReportsWhyAndReturnsNull(string? file, string? text, string message, string rootPath)
    {
        if (file is not null)
            Write(file, text!);

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(rootPath, root.FullName, error);

        Assert.Null(sources);
        Assert.StartsWith(message, error.ToString());
    }

    private void Write(string path, string text)
    {
        string full = Path.Combine(root.FullName, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
