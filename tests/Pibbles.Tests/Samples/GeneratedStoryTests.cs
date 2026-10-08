using Pibbles.Benchmarks;
using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Runtime;
using Pibbles.Semantics;
using Pibbles.Syntax;
using Pibbles.Tests.Syntax;

namespace Pibbles.Tests.Samples;

/// <summary>The benchmarks' generated story: it must compile cleanly, use every construct, and play through to its end.</summary>
public class GeneratedStoryTests
{
    private readonly IReadOnlyList<SourceText> sources = new StoryGenerator(seed: 7).Generate(chapters: 3, scenes: 4);

    [Fact]
    public void Generate_AnySeed_CompilesWithoutErrorsOrWarnings()
    {
        CompileResult result = StoryCompiler.Compile(sources);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity is not DiagnosticSeverity.Hint).Select(diagnostic => $"{diagnostic.Location.Path} {diagnostic.Code} {diagnostic.Message}"));
    }

    [Fact]
    public void Generate_AnySeed_UsesEveryStatementAndInlineElement()
    {
        HashSet<Type> seen = [.. sources.SelectMany(source => NodeFields.DescendantsAndSelf(SyntaxTree.Parse(source).Root)).Select(node => node.GetType())];

        string[] missing =
        [
            .. typeof(SyntaxTree).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && (type.IsSubclassOf(typeof(StatementSyntax)) || type.IsSubclassOf(typeof(InlineSyntax))) && !seen.Contains(type))
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Empty(missing);
    }

    [Fact]
    public void Generate_FirstScene_PlaysThroughEveryChapterToTheEnd()
    {
        Story story = StoryCompiler.Compile(sources).Story!;
        var runner = new DialogueRunner(story, new StoryState(story, 1), new HostFunctions().Add("has_item", (string id) => true).Add("price", (string item) => 1m));
        runner.Start(StoryGenerator.FirstScene);

        int steps = 0;
        for (DialogueStep step = runner.Next(); step is not EndStep; step = runner.Next(), steps++)
        {
            if (step is ChoiceStep choice)
                runner.Choose(choice.Options.Last(option => option.IsAvailable));
        }

        Assert.Equal([3, 4], [story.Nodes.Count(node => node.Name.EndsWith(".aside", StringComparison.Ordinal)), story.Nodes.Count(node => node.Name.StartsWith("ch0.s", StringComparison.Ordinal))]);
        Assert.InRange(steps, 12 * 20, 12 * 200);
    }
}
