using BenchmarkDotNet.Attributes;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Benchmarks;

/// <summary>The generated story every benchmark uses: 50 chapters of 20 scenes, about 3.7 MB of script.</summary>
public static class BigStory
{
    public const int Chapters = 50;
    public const int Scenes = 20;

    public static IReadOnlyList<SourceText> Sources { get; } = new StoryGenerator(seed: 2026).Generate(Chapters, Scenes);

    /// <summary>The story, compiled. It throws if the generator wrote anything that doesn't compile.</summary>
    public static Story Compile(IReadOnlyList<SourceText> sources)
    {
        CompileResult result = StoryCompiler.Compile(sources);
        return result.Story ?? throw new InvalidOperationException($"The generated story has errors: {string.Join("; ", result.Diagnostics.Take(5).Select(diagnostic => diagnostic.Message))}");
    }

    /// <summary>The host functions the generated story declares.</summary>
    public static HostFunctions Functions() => new HostFunctions()
        .Add("has_item", (string id) => id.Length > 3)
        .Add("price", (string item) => 2.5m);
}

/// <summary>Compiling the big story from source: parsing alone, then parsing, binding and checking, then lowering as well.</summary>
[MemoryDiagnoser]
public class CompileBenchmarks
{
    private readonly IReadOnlyList<SourceText> sources = BigStory.Sources;

    [GlobalSetup]
    public void Setup() => Console.WriteLine($"// The story: {sources.Count} files, {StoryGenerator.Megabytes(StoryGenerator.Size(sources))}.");

    [Benchmark]
    public int Parse()
    {
        int nodes = 0;
        foreach (SourceText source in sources)
            nodes += SyntaxTree.Parse(source).Root.Nodes.Count;

        return nodes;
    }

    [Benchmark]
    public Compilation Bind() => Compilation.Create(sources);

    [Benchmark]
    public CompileResult Compile() => StoryCompiler.Compile(sources);
}

/// <summary>The runtime: a step, rendering a line, revealing it, and saving and loading.</summary>
[MemoryDiagnoser]
public class RuntimeBenchmarks
{
    private const int Steps = 10_000;
    private const int Lines = 1_000;

    private const string RichLine = """
        @actor mira
            name: Mira
        @var $name = "Sam"
        @var $count = 3
        @var $flag = false
        @command shake(strength: number = 1) inline
        @markup wave(amplitude: number = 1, frequency: number = 5)
        @markup shout
        @icon key

        == bench.line
        mira: [wave amplitude=2]Waves[/wave] and {$name} has {$count} coins,{w 0.3} then {speed 0.5}slowly{speed} a {icon key} {@shake 2}shakes, {if $flag}yes{else}no{/if}.{w} [b]Bold [shout]and loud[/shout][/b], to the end of a long line. #id:l1
        @jump bench.line
        """;

    private Story story = null!;
    private HostFunctions functions = null!;
    private DialogueRunner lineRunner = null!;
    private Line line = null!;
    private StoryState savedState = null!;
    private DialogueRunner savedRunner = null!;

    [GlobalSetup]
    public void Setup()
    {
        story = BigStory.Compile(BigStory.Sources);
        functions = BigStory.Functions();

        Story lineStory = BigStory.Compile([new("bench.pib", RichLine)]);
        lineRunner = new(lineStory, new StoryState(lineStory, 1), new HostFunctions());
        lineRunner.Start("bench.line");
        line = ((LineStep)lineRunner.Next()).Line;

        savedState = new StoryState(story, 1);
        savedRunner = new DialogueRunner(story, savedState, functions);
        savedRunner.Start(StoryGenerator.FirstScene);
        Walk(savedRunner, 2_000);
        while (savedRunner.Next() is not LineStep)
        {
        }
    }

    /// <summary>The time per <see cref="DialogueRunner.Next"/> over a long walk through the big story, picking the first available option at each choice.</summary>
    [Benchmark(OperationsPerInvoke = Steps)]
    public DialogueRunner Next()
    {
        var runner = new DialogueRunner(story, new StoryState(story, 1), functions);
        runner.Start(StoryGenerator.FirstScene);
        Walk(runner, Steps);
        return runner;
    }

    /// <summary>The time to render a line with spans, markers, an icon, interpolation and conditional text: one <see cref="DialogueRunner.Next"/> that shows it.</summary>
    [Benchmark(OperationsPerInvoke = Lines)]
    public Line RenderLine()
    {
        Line shown = line;
        for (int i = 0; i < Lines; i++)
            shown = ((LineStep)lineRunner.Next()).Line;

        return shown;
    }

    /// <summary>Revealing that line at 30 characters a second, a 60 fps frame at a time, through its waits and command.</summary>
    [Benchmark]
    public int RevealLine()
    {
        var reveal = new LineReveal(line, new RevealSettings(30));
        int frames = 0;
        while (reveal.State is not RevealState.Complete)
        {
            if (reveal.State is RevealState.WaitingForInput or RevealState.WaitingForHost)
                reveal.Resume();

            reveal.Advance(TimeSpan.FromMilliseconds(16));
            frames++;
        }

        return frames;
    }

    /// <summary>Saving the state and the runner of a game well into the big story, as JSON, and loading both back.</summary>
    [Benchmark]
    public DialogueRunner SaveAndLoad()
    {
        string state = SnapshotJson.Serialize(savedState.CreateSnapshot().Value);
        string runner = SnapshotJson.Serialize(savedRunner.CreateSnapshot().Value);
        StoryState loaded = StoryState.Restore(story, SnapshotJson.DeserializeState(state)).Value;
        return DialogueRunner.Restore(story, loaded, functions, SnapshotJson.DeserializeRunner(runner)).Value;
    }

    private static void Walk(DialogueRunner runner, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            switch (runner.Next())
            {
                case ChoiceStep choice:
                    runner.Choose(choice.Options.First(option => option.IsAvailable));
                    break;
                case EndStep:
                    runner.Start(StoryGenerator.FirstScene);
                    break;
            }
        }
    }
}
