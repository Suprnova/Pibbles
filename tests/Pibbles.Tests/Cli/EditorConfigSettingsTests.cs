using Pibbles.Cli.Commands;
using Pibbles.Cli.Projects;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Cli;

public sealed class EditorConfigSettingsTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("pibbles-");
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public EditorConfigSettingsTests()
    {
        Directory.CreateDirectory(Path.Combine(root.FullName, "story", "drafts"));
        Write(".editorconfig", "root = true\n\n[*.pib]\npibbles_diagnostic.PIB3010.severity = error\n");
        Write("story/drafts/.editorconfig", "[*.pib]\npibbles_diagnostic.PIB3010.severity = none\n");
        Write("story/kitchen.pib", "== kitchen.door\nLocked.\n");
        Write("story/drafts/cellar.pib", "== cellar.stairs\nDark.\n");
    }

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        root.Delete(recursive: true);
    }

    [Fact]
    public void Load_NestedFiles_NearerFileOverridesFartherOne()
    {
        IReadOnlyList<SourceText> sources = StoryFolder.Load(".", root.FullName, error)!;
        CompilationOptions options = EditorConfigSettings.Load(sources, root.FullName, error);

        int exitCode = Check.Run(sources, new(OutputFormat.MSBuild), output, options);

        Assert.Equal(Check.Failed, exitCode);
        Assert.Equal("story/kitchen.pib(2,8): error PIB3010: This line has no `#id`.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Load_BadSetting_ReportsItOnce()
    {
        Write("story/drafts/.editorconfig", "[*.pib]\npibbles_diagnostic.PIB9999.severity = none\n");
        Write("story/drafts/attic.pib", "== attic.door\nDusty.\n");
        IReadOnlyList<SourceText> sources = StoryFolder.Load(".", root.FullName, error)!;

        EditorConfigSettings.Load(sources, root.FullName, error);

        Assert.Equal(1, error.ToString().Split('\n').Count(line => line.Contains("PIB9999", StringComparison.OrdinalIgnoreCase)));
    }

    private void Write(string path, string text) => File.WriteAllText(Path.Combine(root.FullName, path), text);
}
