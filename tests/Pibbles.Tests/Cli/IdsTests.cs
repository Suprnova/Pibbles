using System.Text;
using Pibbles.Cli.Commands;

namespace Pibbles.Tests.Cli;

public sealed class IdsTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("pibbles-");
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();
    private readonly Random random = new(1414);
    private readonly string story;

    public IdsTests() => story = Directory.CreateDirectory(Path.Combine(root.FullName, "story")).FullName;

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        root.Delete(recursive: true);
    }

    [Fact]
    public void Run_LinesWithoutIds_WritesIdsKeepingByteOrderMarkAndLineEndings()
    {
        string file = Path.Combine(story, "kitchen.pib");
        File.WriteAllText(file, "== kitchen.door\r\nLocked.\r\nStill locked.\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        int exitCode = Ids.Run(".", root.FullName, random, output, error);

        byte[] written = File.ReadAllBytes(file);
        Assert.Equal((Check.Passed, "Added 2 IDs in 1 file.\n"), (exitCode, output.ToString().ReplaceLineEndings("\n")));
        Assert.Equal([0xEF, 0xBB, 0xBF], written[..3]);
        Assert.Matches("^== kitchen.door\r\nLocked. #id:[a-z0-9]{6}\r\nStill locked. #id:[a-z0-9]{6}\r\n$", Encoding.UTF8.GetString(written[3..]));
    }

    [Fact]
    public void Run_SecondTime_ChangesNothing()
    {
        string file = Path.Combine(story, "kitchen.pib");
        File.WriteAllText(file, "== kitchen.door\nLocked.\n");
        Ids.Run(".", root.FullName, random, output, error);
        string first = File.ReadAllText(file);

        int exitCode = Ids.Run(".", root.FullName, random, output, error);

        Assert.Equal((Check.Passed, first), (exitCode, File.ReadAllText(file)));
        Assert.EndsWith("Every line already has an ID.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_FileWithMistakes_LeavesItAloneAndFails()
    {
        string broken = Path.Combine(story, "broken.pib");
        File.WriteAllText(broken, "== kitchen.door\nLocked.\n@jump\n");
        File.WriteAllText(Path.Combine(story, "clean.pib"), "== kitchen.fridge\nIt hums.\n");

        int exitCode = Ids.Run(".", root.FullName, random, output, error);

        Assert.Equal((Check.Failed, "== kitchen.door\nLocked.\n@jump\n"), (exitCode, File.ReadAllText(broken)));
        Assert.Equal("Added 1 ID in 1 file.\n", output.ToString().ReplaceLineEndings("\n"));
        Assert.EndsWith("  story/broken.pib\n", error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Run_NoStoryFolder_CouldNotRun()
    {
        Directory.Delete(story);

        Assert.Equal(Check.CouldNotRun, Ids.Run(".", root.FullName, random, output, error));
    }
}
