using Pibbles.Compiler;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// What a save slot holds: the story's variables, how often each node has been visited and each variation block
/// entered, which options have been chosen, each actor's pose, and the seed. A <see cref="DialogueRunner"/> changes it
/// as it runs, and several runners can share one.
/// </summary>
public sealed class StoryState
{
    /// <summary>Starts a new game: each variable has its starting value, and nothing has happened yet.</summary>
    /// <param name="story">The story the state is for.</param>
    /// <param name="seed">The seed for the story's randomness, chosen by the host, usually at random for a new game. It's stored and not used yet.</param>
    /// <exception cref="InvalidOperationException">A variable's starting value reads a variable declared after it, or calls a function.</exception>
    public StoryState(Story story, long seed)
    {
        Story = story;
        Seed = seed;

        var context = new StartingContext(Variables);
        foreach (StoryVariable variable in story.Variables)
            Variables[variable.Variable] = Evaluator.Evaluate(variable.StartingValue, context);
    }

    /// <summary>The seed the host chose for this game.</summary>
    public long Seed { get; }

    internal Story Story { get; }

    internal Dictionary<VariableSymbol, Value> Variables { get; } = [];

    internal Dictionary<string, int> Visits { get; } = [];

    internal Dictionary<string, int> BlockEntries { get; } = [];

    internal HashSet<string> ChosenOptions { get; } = [];

    internal Dictionary<string, string> Poses { get; } = [];

    /// <summary>
    /// An actor's current pose: the last one the story set, or the actor's first pose if it hasn't set one. A host can
    /// use it to redraw after a load.
    /// </summary>
    /// <param name="actor">The actor's ID.</param>
    /// <returns>The pose's name, or <see langword="null"/> if the actor has no poses.</returns>
    /// <exception cref="ArgumentException">The story has no actor with that ID.</exception>
    public string? GetPose(string actor)
    {
        if (Poses.TryGetValue(actor, out string? pose))
            return pose;

        return Story.Actors.TryGetValue(actor, out ActorSymbol? declared)
            ? (declared.Poses is [var first, ..] ? first.Name : null)
            : throw new ArgumentException($"The story has no actor called `{actor}`.", nameof(actor));
    }

    /// <summary>Evaluates starting values, which can only read the variables before them.</summary>
    private sealed class StartingContext(Dictionary<VariableSymbol, Value> variables) : IEvaluationContext
    {
        public Value GetVariable(VariableSymbol variable) =>
            variables.TryGetValue(variable, out Value value)
                ? value
                : throw new InvalidOperationException($"The starting value of a variable reads `${variable.Name}`, which is declared after it.");

        public int GetVisits(string node) => 0;

        public Value CallFunction(CallExpr call, IReadOnlyList<Value> arguments) =>
            throw new InvalidOperationException($"The starting value of a variable calls `{call.Function.Name}`, but nothing has been registered yet.");

        public void Warn(RuntimeWarning warning)
        {
        }
    }
}
