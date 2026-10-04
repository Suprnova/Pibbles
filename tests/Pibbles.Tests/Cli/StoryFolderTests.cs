using Pibbles.Cli;
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
    public void Load_StorySetting_ReadsThatFolder()
    {
        Write("pibbles.json", """{ "version": "1.0", "story": "dialogue" }""");
        Write("dialogue/a.pib", "== a");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load(".", root.FullName, error);

        Assert.Equal(["dialogue/a.pib"], sources!.Select(source => source.Path));
    }

    [Fact]
    public void Load_RootBelowCurrentDirectory_ShowsPathsFromCurrentDirectory()
    {
        Write("game/story/a.pib", "== a");

        IReadOnlyList<SourceText>? sources = StoryFolder.Load("game", root.FullName, error);

        Assert.Equal(["game/story/a.pib"], sources!.Select(source => source.Path));
    }

    [Theory]
    [InlineData(null, null, "I can't find the folder `missing`.", "missing")]
    [InlineData(null, null, "I can't find the story folder `story`.", ".")]
    [InlineData("pibbles.json", "{ not json", "I can't read `pibbles.json`:", ".")]
    [InlineData("pibbles.json", """{ "story": 3 }""", "`story` in `pibbles.json` has to be a folder name", ".")]
    [InlineData("story/readme.txt", "no sources", "There are no `.pib` files in `story`.", ".")]
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
