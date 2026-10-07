using System.Text;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Syntax;
using static VerifyXunit.Verifier;

namespace Pibbles.Tests.Runtime;

/// <summary>Plays every branch of the sample story through the runner and compares the runs with a snapshot.</summary>
public class KitchenRunTests
{
    private static readonly string Directory = Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Runtime", "Snapshots");

    [Fact]
    public Task Play_EveryBranchOfTheKitchenStory_MatchesTheRecordedRuns()
    {
        var output = new StringBuilder();

        Section(output, "enter, first visit", Kitchen(), game => game.Play("kitchen.enter"));
        Section(output, "enter, second visit", Kitchen(), game =>
        {
            game.Play("kitchen.enter");
            return game.Play("kitchen.enter");
        });
        Section(output, "fridge, four visits", Kitchen(), game => [.. Enumerable.Range(0, 4).SelectMany(_ => game.Play("kitchen.fridge"))]);
        Section(output, "drawer, two visits", Kitchen(), game => [.. game.Play("kitchen.drawer"), .. game.Play("kitchen.drawer")]);
        Section(output, "window with the crowbar", Kitchen(crowbar: true), game => game.Play("kitchen.window"));
        Section(output, "window without it, five visits", Kitchen(), game => [.. Enumerable.Range(0, 5).SelectMany(_ => game.Play("kitchen.window"))]);
        Section(output, "door, open already", Kitchen(), game =>
        {
            game.Set("door_open", Value.Bool(true));
            return game.Play("kitchen.door");
        });
        Section(output, "door, the options", Kitchen(), game => game.Play("kitchen.door"));
        Section(output, "door, rattle the handle", Kitchen(), game => game.Play("kitchen.door", "Rattle the handle"));
        Section(output, "door, rattle when brave", Kitchen(), game =>
        {
            game.Set("bravery", Value.Number(3));
            return game.Play("kitchen.door", "Rattle the handle");
        });
        Section(output, "door, knock twice", Kitchen(), game => [.. game.Play("kitchen.door", "Knock politely"), .. game.Play("kitchen.door")]);
        Section(output, "door, use the key", Kitchen(), game =>
        {
            game.Set("has_key", Value.Bool(true));
            return game.Play("kitchen.door", "Use the key");
        });
        Section(output, "door, leave it", Kitchen(), game => game.Play("kitchen.door", "Leave it"));
        Section(output, "leave", Kitchen(), game => game.Play("kitchen.leave"));
        Section(output, "old name of the door", Kitchen(), game => game.Play("kitchen.front_door", "Leave it"));

        return Verify(output.ToString()).UseDirectory(Directory).UseFileName("kitchen-runs");
    }

    private static void Section(StringBuilder output, string title, Game game, Func<Game, string[]> play)
    {
        output.Append("== ").Append(title).Append('\n');
        foreach (string line in play(game))
            output.Append("  ").Append(line).Append('\n');

        output.Append('\n');
    }

    private static Game Kitchen(bool crowbar = false)
    {
        string folder = Path.Combine(RepositoryRoot.Path, "samples", "kitchen");
        SourceText[] sources =
        [
            .. System.IO.Directory.EnumerateFiles(folder, "*.pib", SearchOption.AllDirectories)
                .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/'), File.ReadAllText(path))),
        ];
        Story story = StoryCompiler.Compile(sources).Story ?? throw new InvalidOperationException("The kitchen story has errors.");
        return Game.Of(story, functions => functions.Add("has_item", (string id) => crowbar && id == "crowbar"));
    }
}
