using Pibbles.Compiler;
using Pibbles.Configuration;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Compiler;

public class StoryCompilerTests
{
    private static readonly SourceText WithError = new("story.pib", "== a.b\n@set $missing = 1\nNarration.\n");

    private static readonly SourceText WithWarning = new("story.pib", "== a.b\nNarration.\n");

    [Fact]
    public void Compile_KitchenStory_GivesAStory()
    {
        SourceText[] sources =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "samples", "kitchen"), "*.pib", SearchOption.AllDirectories)
                .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path), File.ReadAllText(path))),
        ];

        CompileResult result = StoryCompiler.Compile(sources);

        Assert.NotNull(result.Story);
        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_StoryWithAnError_GivesNoStoryAndTheCompilationsDiagnostics()
    {
        CompileResult result = StoryCompiler.Compile([WithError]);

        Assert.Null(result.Story);
        Assert.True(result.HasErrors);
        Assert.Equal(Compilation.Create([WithError]).Diagnostics, result.Diagnostics);
    }

    [Fact]
    public void Compile_StoryWithOnlyWarnings_GivesAStory()
    {
        CompileResult result = StoryCompiler.Compile([WithWarning]);

        Assert.NotNull(result.Story);
        Assert.False(result.HasErrors);
        Assert.Equal(["PIB3010"], result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void Compile_WarningRaisedToErrorBySettings_GivesNoStory()
    {
        FileSettings settings = FileSettings.From([KeyValuePair.Create("pibbles_diagnostic.PIB3010.severity", "error")]);
        var options = new CompilationOptions(new Dictionary<string, FileSettings> { ["story.pib"] = settings });

        CompileResult result = StoryCompiler.Compile([WithWarning], options);

        Assert.Null(result.Story);
        Assert.True(result.HasErrors);
    }
}
