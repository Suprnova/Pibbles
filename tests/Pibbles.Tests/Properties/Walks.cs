using CsCheck;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// A random walk over a real story: which story, where it starts, which available option it picks at each choice, and
/// what each host function returns. The stories are the sample story and the sources of the transcript tests.
/// </summary>
/// <param name="Story">Which of <see cref="Walks.Stories"/>.</param>
/// <param name="Start">Which of the story's nodes to start at.</param>
/// <param name="Picks">At the <c>n</c>th choice, the walk picks available option <c>Picks[n % Picks.Length]</c>, wrapping around.</param>
/// <param name="Stubs">Each declared function always returns the value <see cref="Walks.StubValue"/> makes of its entry.</param>
internal sealed record Walk(int Story, int Start, int[] Picks, int[] Stubs)
{
    public static Gen<Walk> Gen { get; } = CsCheck.Gen.Select(
        CsCheck.Gen.Int[0, 1000],
        CsCheck.Gen.Int[0, 1000],
        CsCheck.Gen.Int[0, 1000].Array[1, 12],
        CsCheck.Gen.Int[0, 1000].Array[1, 6],
        (story, start, picks, stubs) => new Walk(story % Walks.Stories.Count, start, picks, stubs));

    public Story Compiled => Walks.Stories[Story];

    public string Node => Compiled.Nodes[Start % Compiled.Nodes.Count].Name;

    /// <summary>The value each declared function returns, as a host value.</summary>
    public IEnumerable<(FunctionInfo Function, object Value)> StubValues =>
        Compiled.Functions.Select((function, index) => (function, Walks.StubValue(Compiled, function.ReturnType, Stubs[index % Stubs.Length])));

    /// <summary>A fresh chooser, which picks from <see cref="Picks"/> in turn.</summary>
    public Func<ChoiceStep, ChoiceOption> Chooser()
    {
        int choices = 0;
        return choice =>
        {
            ChoiceOption[] available = [.. choice.Options.Where(option => option.IsAvailable)];
            return available[Picks[choices++ % Picks.Length] % available.Length];
        };
    }

    public override string ToString() =>
        $"story {Story} from {Node}, picks [{string.Join(',', Picks)}], stubs [{string.Join(", ", StubValues.Select(stub => $"{stub.Function.Name} = {stub.Value}"))}]";
}

internal static class Walks
{
    public static IReadOnlyList<Story> Stories { get; } =
    [
        Compile(Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "samples", "kitchen", "story"), "*.pib")),
        .. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Transcripts"), "*.pib").Order(StringComparer.Ordinal).Select(path => Compile([path])),
    ];

    /// <summary>A value of a type, as a host value, picked by <paramref name="n"/>: negative numbers and zero included.</summary>
    public static object StubValue(Story story, StoryType type, int n) => type.Kind switch
    {
        StoryTypeKind.Bool => n % 2 == 0,
        StoryTypeKind.Number => (decimal)(n % 7) - 2,
        StoryTypeKind.Duration => TimeSpan.FromMilliseconds(n % 5 * 100),
        StoryTypeKind.Text => $"s{n % 3}",
        StoryTypeKind.Enum => Pick(story.Enums.First(@enum => @enum.Name == type.EnumName).Members, n),
        StoryTypeKind.Actor => Pick(story.Actors, n).Id,
        _ => Pick(story.Nodes, n).Name,
    };

    private static T Pick<T>(IReadOnlyList<T> items, int n) => items[n % items.Count];

    private static Story Compile(IEnumerable<string> paths)
    {
        SourceText[] sources = [.. paths.Order(StringComparer.Ordinal).Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/'), File.ReadAllText(path)))];
        return StoryCompiler.Compile(sources).Story ?? throw new InvalidOperationException($"{sources[0].Path} doesn't compile.");
    }
}
