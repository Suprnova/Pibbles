using System.Globalization;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Runtime;

/// <summary>A compiled story with a state and a runner, and a way to play it and read the run as text.</summary>
internal sealed class Game
{
    /// <summary>Declarations the test stories share.</summary>
    public const string Defs = """
        @enum mood: calm, tense
        @actor mira
            name: Mira
            poses: neutral, happy
        @actor rex
            name: Rex
        @var $count = 0
        @var $key = false
        @var $name = "Sam"
        @var $price = 1.50
        @var $delay = 0s
        @var $who: actor = rex
        @command show(who: actor, at: mood = calm)
        @command shake(strength: number = 1, duration: duration = 0.3s) inline
        @command all(b: bool, s: string, n: number, d: duration, a: actor, node: node, e: mood = calm)
        @function has_item(id: string) -> bool
        @function risky() -> number
        @icon bag
        @markup shout
        @markup wave(amplitude: number = 1, frequency: number = 5)
        @tag thought
        @tag box: mood
        @tag cue: string

        """;

    private Game(Story story, HostFunctions functions, RunnerOptions options, List<RuntimeWarning> warnings)
    {
        Story = story;
        Functions = functions;
        Warnings = warnings;
        State = new(story, 1);
        Runner = new(story, State, functions, options);
    }

    public Story Story { get; }

    public HostFunctions Functions { get; }

    public StoryState State { get; }

    public DialogueRunner Runner { get; }

    public List<RuntimeWarning> Warnings { get; }

    /// <summary>Compiles <paramref name="nodes"/> after the shared declarations.</summary>
    public static Game Of(string nodes, Action<HostFunctions>? register = null, int budget = 100_000, bool declarations = true)
    {
        CompileResult result = StoryCompiler.Compile([new("story.pib", (declarations ? Defs : "") + nodes)]);
        Story story = result.Story ?? throw new Xunit.Sdk.XunitException(string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")));
        return Of(story, register, budget);
    }

    public static Game Of(Story story, Action<HostFunctions>? register = null, int budget = 100_000)
    {
        List<RuntimeWarning> warnings = [];
        var functions = new HostFunctions();
        (register ?? (functions => functions.Add("has_item", (string id) => id == "crowbar")))(functions);
        return new(story, functions, new() { InstructionBudget = budget, OnWarning = warnings.Add }, warnings);
    }

    /// <summary>Starts a node and plays it to its end, choosing the options named in <paramref name="picks"/> (by text) in turn. Stops at a choice with no pick left.</summary>
    public string[] Play(string node, params string[] picks)
    {
        Runner.Start(node);
        return Continue(picks);
    }

    /// <summary>Plays on from where the runner is.</summary>
    public string[] Continue(params string[] picks)
    {
        Queue<string> remaining = new(picks);
        List<string> lines = [];
        while (true)
        {
            DialogueStep step = Runner.Next();
            lines.Add(Format(step));
            switch (step)
            {
                case EndStep:
                    return [.. lines];
                case ChoiceStep choice:
                    if (remaining.Count == 0)
                        return [.. lines];

                    string pick = remaining.Dequeue();
                    Runner.Choose(choice.Options.First(option => option.Text.Text == pick));
                    lines.Add($"Chose {pick}");
                    break;
            }
        }
    }

    public void Set(string variable, Value value) => State.Variables[Story.VariableDefinitions.First(candidate => candidate.Variable.Name == variable).Variable] = value;

    public Value Get(string variable) => State.Variables[Story.VariableDefinitions.First(candidate => candidate.Variable.Name == variable).Variable];

    public static string Format(DialogueStep step) => step switch
    {
        LineStep line => $"Line{(line.Line.Speaker is { } speaker ? $" {speaker}" : "")}: {Visible(line.Line.Text)}",
        ChoiceStep choice => "Choice " + string.Join(" | ", choice.Options.Select(option => option.Text.Text + (option.IsAvailable ? "" : " (unavailable)") + (option.WasChosen ? " (chosen)" : ""))),
        CommandStep command => $"Command @{command.Command.Name}({string.Join(", ", command.Command.Values.Select(Show))}){(command.Waits ? " waits" : "")}",
        PoseStep pose => $"Pose {pose.Actor} {pose.Pose}",
        WaitStep wait => $"Wait {wait.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s",
        EndStep => "End",
        _ => throw new NotSupportedException(step.GetType().Name),
    };

    private static string Visible(string text) => text.Replace("\n", "\\n", StringComparison.Ordinal).Replace("￼", "<icon>", StringComparison.Ordinal);

    private static string Show(Value value) =>
        value.Type == TypeSymbol.Bool ? (value.AsBool ? "true" : "false")
        : value.Type == TypeSymbol.Number ? value.AsDecimal.ToString(CultureInfo.InvariantCulture)
        : value.Type == TypeSymbol.Duration ? value.AsDecimal.ToString(CultureInfo.InvariantCulture) + "s"
        : value.Type == TypeSymbol.String ? $"\"{value.AsString}\""
        : value.Type == TypeSymbol.Node ? value.AsString
        : value.AsSymbol.Name;
}
