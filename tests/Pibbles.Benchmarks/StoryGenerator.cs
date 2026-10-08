using System.Globalization;
using System.Text;
using Pibbles.Syntax;

namespace Pibbles.Benchmarks;

/// <summary>
/// Writes a story the size of a big visual novel, the same every time for the same seed: a declarations file, then
/// chapters of scenes that use every statement and every inline element, each line with an ID. The scenes chain by
/// <c>@jump</c> from the first to the last, so a walk can run through all of them.
/// </summary>
/// <remarks>The test project compiles this file too, and checks that what it writes compiles without errors or warnings.</remarks>
public sealed class StoryGenerator(int seed)
{
    /// <summary>The first scene, where a walk through the whole story starts.</summary>
    public const string FirstScene = "ch0.s0";

    private const int Flags = 8;
    private const int Counts = 8;

    private static readonly string[] Actors = ["mira", "rex", "ana", "bo", "cy", "dee"];
    private static readonly string[] Poses = ["neutral", "happy", "sad", "angry"];
    private static readonly string[] Places = ["left", "center", "right"];

    private static readonly string[] Words =
    [
        "the", "kitchen", "door", "was", "never", "quite", "closed", "and", "nobody", "knew", "why", "she", "looked", "at",
        "fridge", "again", "hummed", "quietly", "maybe", "tomorrow", "said", "nothing", "brass", "key", "spoons", "window",
        "dusty", "light", "under", "table", "rain", "outside", "listen", "wait", "careful", "honestly", "strange", "warm",
    ];

    private readonly Random random = new(seed);
    private int ids;

    /// <summary>Writes the story: <c>defs.pib</c>, then one file per chapter.</summary>
    /// <param name="chapters">How many chapter files.</param>
    /// <param name="scenes">How many scenes each chapter has.</param>
    public IReadOnlyList<SourceText> Generate(int chapters, int scenes)
    {
        List<SourceText> sources = [new("story/defs.pib", Declarations())];
        for (int chapter = 0; chapter < chapters; chapter++)
            sources.Add(new($"story/chapter{chapter:D3}.pib", Chapter(chapter, scenes, last: chapter == chapters - 1)));

        return sources;
    }

    private static string Declarations()
    {
        var text = new StringBuilder();
        foreach (string actor in Actors)
            text.Append(CultureInfo.InvariantCulture, $"@actor {actor}\n    name: {char.ToUpperInvariant(actor[0])}{actor[1..]}\n    poses: {string.Join(", ", Poses)}\n");

        text.Append("@enum position: left, center, right\n@enum mood: calm, tense, wild\n");
        for (int flag = 0; flag < Flags; flag++)
            text.Append(CultureInfo.InvariantCulture, $"@var $flag{flag} = false\n");

        for (int count = 0; count < Counts; count++)
            text.Append(CultureInfo.InvariantCulture, $"@var $count{count} = 0\n");

        text.Append("""
            @var $name = "Sam"
            @var $delay = 0.2s
            @var $who: actor = mira
            @var $mood: mood = calm
            @command show(who: actor, at: position = center)
            @command hide(who: actor)
            @command move(who: actor, to: position) waits
            @command shake(strength: number = 1) inline
            @command camera(target: string) waits
            @markup wave(amplitude: number = 1, frequency: number = 5)
            @markup shout
            @icon key, bag
            @tag thought
            @tag box: mood
            @function has_item(id: string) -> bool
            @function price(item: string) -> number

            """);
        return text.ToString();
    }

    private string Chapter(int chapter, int scenes, bool last)
    {
        var text = new StringBuilder($"// Chapter {chapter}.\n\n");
        text.Append(CultureInfo.InvariantCulture, $"== ch{chapter}.aside\n");
        for (int line = 0; line < 3; line++)
            text.Append(Line(""));

        text.Append(CultureInfo.InvariantCulture, $"@set $mood = {Pick(["calm", "tense", "wild"])}\n@return\n\n");
        for (int scene = 0; scene < scenes; scene++)
        {
            string next = scene < scenes - 1 ? $"ch{chapter}.s{scene + 1}" : last ? "" : $"ch{chapter + 1}.s0";
            Scene(text, chapter, scene, next);
        }

        return text.ToString();
    }

    private void Scene(StringBuilder text, int chapter, int scene, string next)
    {
        string name = $"ch{chapter}.s{scene}";
        string actor = Pick(Actors);
        string flag = $"$flag{random.Next(Flags)}";
        string count = $"$count{random.Next(Counts)}";

        text.Append(CultureInfo.InvariantCulture, $"== {name}\n");
        text.Append(CultureInfo.InvariantCulture, $"@show {actor} {Pick(Places)}\n");
        text.Append(CultureInfo.InvariantCulture, $"{actor} ({Pick(Poses)}): {Text()} #id:{Id()}\n");
        for (int line = random.Next(6, 12); line > 0; line--)
            text.Append(Line(""));

        text.Append(CultureInfo.InvariantCulture, $"@if {flag}\n{Line("    ")}@elif {count} > 3\n{Line("    ")}@else\n{Line("    ")}");
        text.Append(CultureInfo.InvariantCulture, $"@set {count} += {random.Next(1, 3)}\n");
        text.Append(CultureInfo.InvariantCulture, $"@set $name = \"{Pick(Words)}\"\n");
        text.Append(CultureInfo.InvariantCulture, $"@sequence #id:{Id()}\n    - {Line("").TrimEnd('\n')}\n    - {Line("").TrimEnd('\n')}\n");
        text.Append(CultureInfo.InvariantCulture, $"@cycle #id:{Id()}\n    - {Line("").TrimEnd('\n')}\n    - {Line("").TrimEnd('\n')}\n    - {Line("").TrimEnd('\n')}\n");
        text.Append(CultureInfo.InvariantCulture, $"@once #id:{Id()}\n{Line("    ")}    @set {flag} = true\n");
        text.Append(CultureInfo.InvariantCulture, $"@move {actor} {Pick(Places)}{(random.Next(2) == 0 ? " nowait" : "")}\n");
        text.Append(CultureInfo.InvariantCulture, $"@wait {(random.Next(2) == 0 ? "0.3s" : "$delay")}\n");
        text.Append(CultureInfo.InvariantCulture, $"@call ch{chapter}.aside #id:{Id()}\n");
        text.Append(CultureInfo.InvariantCulture, $"@if visits({name}) > 1 and has_item(\"key\")\n{Line("    ")}");
        for (int line = random.Next(6, 12); line > 0; line--)
            text.Append(Line(""));

        text.Append(CultureInfo.InvariantCulture, $"-> {Phrase(3)}  @if {flag} #thought #id:{Id()}\n{Line("    ")}    @set {count} -= 1\n");
        text.Append(CultureInfo.InvariantCulture, $"-> {Phrase(4)}  @once #id:{Id()}\n{Line("    ")}");
        text.Append(CultureInfo.InvariantCulture, $"-> {Phrase(2)} #id:{Id()}\n{Line("    ")}    @camera \"door\"\n");
        text.Append(CultureInfo.InvariantCulture, $"{Pick(Actors)}: {Text()} #box:{Pick(["calm", "tense", "wild"])} #id:{Id()}\n");
        text.Append(CultureInfo.InvariantCulture, $"@hide {actor}\n");
        text.Append(next.Length > 0 ? $"@jump {next}\n\n" : "@end\n\n");
    }

    /// <summary>A text line, spoken or narrated, sometimes posed.</summary>
    private string Line(string indent) => random.Next(5) switch
    {
        0 => $"{indent}{Text()} #id:{Id()}\n",
        1 => $"{indent}{Pick(Actors)} ({Pick(Poses)}): {Text()} #id:{Id()}\n",
        _ => $"{indent}{Pick(Actors)}: {Text()} #id:{Id()}\n",
    };

    /// <summary>A sentence of words with inline elements between them: markup, values, pauses, speed, breaks, icons, commands and conditional text.</summary>
    private string Text()
    {
        var text = new StringBuilder(Capitalized(Pick(Words)));
        for (int word = random.Next(6, 16); word > 0; word--)
        {
            text.Append(' ');
            text.Append(random.Next(10) == 0 ? Element() : Pick(Words));
        }

        return text.Append('.').ToString();
    }

    private string Element() => random.Next(14) switch
    {
        0 => $"[b]{Phrase(2)}[/b]",
        1 => $"[wave amplitude=2]{Phrase(2)}[/wave]",
        2 => $"[shout]{Pick(Words)}[/shout]",
        3 => Pick(["{$name}", "{$who}"]),
        4 => $"{{$count{random.Next(Counts)}}}",
        5 => "{w 0.3}" + Pick(Words),
        6 => "{w}" + Pick(Words),
        7 => Pick(Words) + "{br}" + Pick(Words),
        8 => $"{{speed 0.5}}{Phrase(2)}{{speed}}",
        9 => $"{{icon {Pick(["key", "bag"])}}}",
        10 => "{@shake 2}" + Pick(Words),
        11 => $"{{if $flag{random.Next(Flags)}}}{Pick(Words)}{{else}}{Pick(Words)}{{/if}}",
        12 => Pick(["{price(\"rope\")}", "{if $mood == tense}tense{else}calm{/if}"]),
        _ => Pick(Words) + "{p}" + Pick(Words),
    };

    private string Phrase(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => Pick(Words)));

    private static string Capitalized(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private string Pick(string[] items) => items[random.Next(items.Length)];

    /// <summary>A new line ID: <c>g</c> and five base-36 digits, so it never repeats and never looks like another kind of name.</summary>
    private string Id()
    {
        const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var id = new char[6];
        id[0] = 'g';
        for (int i = 5, n = ids++; i > 0; i--, n /= 36)
            id[i] = Digits[n % 36];

        return new string(id);
    }

    /// <summary>The total size of a story's sources, in bytes of UTF-8.</summary>
    public static long Size(IEnumerable<SourceText> sources) => sources.Sum(source => (long)Encoding.UTF8.GetByteCount(source.Text));

    /// <summary>A size in megabytes, for printing.</summary>
    public static string Megabytes(long bytes) => (bytes / 1_048_576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
}
