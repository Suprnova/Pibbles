using Pibbles.Compiler;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Compiler;

public class StoryLowererTests
{
    [Fact]
    public void Compile_TwoSourcesWithTheSamePath_ThrowsInsteadOfOverwritingAnId()
    {
        SourceText[] sources = [new("story.pib", "== a.one\nHi.\n"), new("story.pib", "== a.two\nHello.\n")];

        var exception = Assert.Throws<InvalidOperationException>(() => StoryCompiler.Compile(sources));

        Assert.Contains("~story.pib:2", exception.Message);
    }

    [Fact]
    public void Lower_DefaultThatWasNeverBound_NamesTheFileItIsDeclaredIn()
    {
        SourceText[] sources =
        [
            new("defs.pib", "@command wave(times: number = $this_default_is_much_longer_than_the_story_that_uses_it)\n"),
            new("story.pib", "== a.b\n@wave\n"),
        ];
        Compilation compilation = Compilation.Create(sources);

        var exception = Assert.Throws<InvalidOperationException>(() => StoryLowerer.Lower(compilation));

        Assert.Contains("defs.pib:1", exception.Message);
    }
}
