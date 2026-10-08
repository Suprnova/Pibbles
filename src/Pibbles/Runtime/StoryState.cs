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
        foreach (StoryVariable variable in story.VariableDefinitions)
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

        return Story.ActorSymbols.TryGetValue(actor, out ActorSymbol? declared)
            ? (declared.Poses is [var first, ..] ? first.Name : null)
            : throw new ArgumentException($"The story has no actor called `{actor}`.", nameof(actor));
    }

    /// <summary>
    /// A variable's current value as a host sees it, by the mapping of <see cref="StoryType.HostType"/>. A duration
    /// variable beyond <see cref="TimeSpan"/>'s range clamps silently without a warning.
    /// </summary>
    /// <param name="variable">The variable's name, without the <c>$</c>.</param>
    /// <exception cref="ArgumentException">The story has no such variable.</exception>
    public object GetVariable(string variable) => HostValues.ToHost(Variables[Find(variable)], default, _ => { });

    /// <summary>Sets a variable from the host, checked against the variable's type.</summary>
    /// <param name="variable">The variable's name, without the <c>$</c>.</param>
    /// <param name="value">The new value, as <see cref="StoryType.HostType"/> says: for an enum member, an actor or a node, a string that names one.</param>
    /// <exception cref="ArgumentException">The story has no such variable, the value isn't of its type, or a string names nothing of the variable's type.</exception>
    public void SetVariable(string variable, object value)
    {
        VariableSymbol symbol = Find(variable);
        try
        {
            Variables[symbol] = HostValues.FromHost(value, symbol.Type, Story, $"`${variable}` was set to");
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException(exception.Message, nameof(value));
        }
    }

    /// <summary>
    /// Takes a snapshot of everything in the state, keyed so that it survives edits to the story: variables by name, visits
    /// by node name, block entries and chosen options by ID, and poses by actor ID.
    /// </summary>
    /// <returns>
    /// The snapshot, and a <see cref="SaveProblemKind.FallbackId"/> problem for each block entry count or chosen option it
    /// left out because its ID was made up by the compiler. That ID changes when the file is edited, so saving it could hand
    /// one line's state to another. Only a story with missing <c>#id</c>s has them.
    /// </returns>
    public SaveResult<StateSnapshot> CreateSnapshot()
    {
        List<SaveProblem> problems = [];
        bool Keep(string id, string what)
        {
            if (!Story.FallbackIds.Contains(id))
                return true;

            problems.Add(new(SaveProblemKind.FallbackId, id, $"{what} `{id}` has no `#id`, so the snapshot leaves it out. Run `pibbles ids` to give it one."));
            return false;
        }

        var snapshot = new StateSnapshot
        {
            Format = StateSnapshot.CurrentFormat,
            Seed = Seed,
            Variables = Story.VariableDefinitions.ToDictionary(definition => definition.Variable.Name, definition => Save(Variables[definition.Variable])),
            Visits = Sorted(Visits),
            BlockEntries = Sorted(BlockEntries.Where(entry => Keep(entry.Key, "The entry count of the block"))),
            ChosenOptions = [.. ChosenOptions.Order(StringComparer.Ordinal).Where(id => Keep(id, "The chosen option"))],
            Poses = Sorted(Poses),
        };
        return new(snapshot, problems);
    }

    /// <summary>
    /// Restores a state from a snapshot, against the story as it is now, which may have changed since the snapshot was taken.
    /// Whatever no longer fits is dropped and reported, never thrown: a variable that's gone or whose type changed, a value
    /// that names something that's gone, and counts, chosen options and poses for nodes, blocks, options, actors and poses
    /// that are gone. A saved node name finds its node through <c>#was:</c> aliases too. Variables the snapshot doesn't
    /// have, or whose value was dropped, keep their starting values.
    /// </summary>
    /// <param name="story">The story as it is now.</param>
    /// <param name="snapshot">The snapshot, in any format up to <see cref="StateSnapshot.CurrentFormat"/>.</param>
    /// <returns>The restored state, and the problems: what was dropped, and what was done instead.</returns>
    /// <exception cref="ArgumentException">The snapshot's format is newer than this version of Pibbles reads.</exception>
    /// <exception cref="InvalidOperationException">A variable's starting value reads a variable declared after it, or calls a function.</exception>
    public static SaveResult<StoryState> Restore(Story story, StateSnapshot snapshot)
    {
        SnapshotChecks.Check(snapshot, nameof(snapshot));
        var state = new StoryState(story, snapshot.Seed);
        List<SaveProblem> problems = [];
        state.RestoreVariables(snapshot.Variables, problems);
        state.RestoreVisits(snapshot.Visits, problems);
        state.RestoreBlockEntries(snapshot.BlockEntries, problems);
        state.RestoreChosenOptions(snapshot.ChosenOptions, problems);
        state.RestorePoses(snapshot.Poses, problems);
        return new(state, problems);
    }

    private void RestoreVariables(IReadOnlyDictionary<string, SavedValue> saved, List<SaveProblem> problems)
    {
        foreach ((string name, SavedValue value) in saved)
        {
            VariableSymbol? variable = Story.VariableDefinitions.Select(definition => definition.Variable).FirstOrDefault(candidate => candidate.Name == name);
            if (variable is null)
                problems.Add(new(SaveProblemKind.UnknownVariable, name, $"The save has `${name}`, which the story no longer declares, so its value is dropped."));
            else if (value.Type != StoryType.Of(variable.Type).ToString())
                problems.Add(new(SaveProblemKind.VariableTypeChanged, name, $"The save has `${name}` as {value.Type}, but it's now {StoryType.Of(variable.Type)}, so it keeps its starting value."));
            else if (Load(value, variable.Type) is { } loaded)
                Variables[variable] = loaded;
            else
                problems.Add(new(SaveProblemKind.InvalidValue, name, $"The save's value of `${name}` is no longer {value.Type}{(value.Text is null ? "" : $": `{value.Text}`")}, so it keeps its starting value."));
        }
    }

    private void RestoreVisits(IReadOnlyDictionary<string, int> saved, List<SaveProblem> problems)
    {
        Dictionary<string, string> landed = [];
        foreach ((string name, int count) in saved)
        {
            string? node = Story.CompiledNodes.ContainsKey(name) ? name : Story.Aliases.GetValueOrDefault(name);
            if (node is null)
            {
                problems.Add(new(SaveProblemKind.UnknownNode, name, $"The save counts visits to `{name}`, which is no longer a node's name or old name, so they're dropped."));
            }
            else if (count < 0)
            {
                problems.Add(InvalidCount(name, count));
            }
            else
            {
                if (landed.TryGetValue(node, out string? other))
                    problems.Add(new(SaveProblemKind.VisitsMerged, name, $"The save counts visits to both `{other}` and `{name}`, which are now the same node `{node}`, so its count is their sum."));

                landed[node] = name;
                Visits[node] = Visits.GetValueOrDefault(node) + count;
            }
        }
    }

    private void RestoreBlockEntries(IReadOnlyDictionary<string, int> saved, List<SaveProblem> problems)
    {
        foreach ((string id, int count) in saved)
        {
            if (Story.InstructionAt(id) is not VariationInstruction)
                problems.Add(new(SaveProblemKind.UnknownBlock, id, $"The save counts entries to the block `{id}`, which no longer exists, so they're dropped."));
            else if (count < 0)
                problems.Add(InvalidCount(id, count));
            else
                BlockEntries[id] = count;
        }
    }

    private void RestoreChosenOptions(IReadOnlyList<string> saved, List<SaveProblem> problems)
    {
        foreach (string id in saved)
        {
            if (Story.InstructionAt(id) is ChoiceInstruction)
                ChosenOptions.Add(id);
            else
                problems.Add(new(SaveProblemKind.UnknownOption, id, $"The save records the option `{id}` as chosen, but it no longer exists, so that's dropped."));
        }
    }

    private void RestorePoses(IReadOnlyDictionary<string, string> saved, List<SaveProblem> problems)
    {
        foreach ((string actor, string pose) in saved)
        {
            if (!Story.ActorSymbols.TryGetValue(actor, out ActorSymbol? declared))
                problems.Add(new(SaveProblemKind.UnknownActor, actor, $"The save has `{actor}` in the pose `{pose}`, but the story no longer declares that actor, so it's dropped."));
            else if (declared.Poses.All(candidate => candidate.Name != pose))
                problems.Add(new(SaveProblemKind.UnknownPose, actor, $"The save has `{actor}` in the pose `{pose}`, which the actor no longer has, so it keeps its default pose."));
            else
                Poses[actor] = pose;
        }
    }

    private static SaveProblem InvalidCount(string subject, int count) =>
        new(SaveProblemKind.InvalidValue, subject, $"The save counts {count} for `{subject}`, which can't be a count, so it's dropped.");

    private static Dictionary<string, TValue> Sorted<TValue>(IEnumerable<KeyValuePair<string, TValue>> entries) =>
        entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary();

    private static SavedValue Save(Value value)
    {
        string type = StoryType.Of(value.Type).ToString();
        return value.Type == TypeSymbol.Bool ? new() { Type = type, Bool = value.AsBool }
            : value.Type == TypeSymbol.Number || value.Type == TypeSymbol.Duration ? new() { Type = type, Number = value.AsDecimal }
            : value.Type == TypeSymbol.String || value.Type == TypeSymbol.Node ? new() { Type = type, Text = value.AsString }
            : new() { Type = type, Text = value.AsSymbol.Name };
    }

    private Value? Load(SavedValue saved, TypeSymbol type) =>
        type == TypeSymbol.Bool ? (saved.Bool is { } flag ? Value.Bool(flag) : null)
        : type == TypeSymbol.Number || type == TypeSymbol.Duration ? (saved.Number is { } number ? Value.Numeric(type, number) : null)
        : saved.Text is { } text ? HostValues.FromText(text, type, Story)
        : null;

    private VariableSymbol Find(string variable) =>
        Story.VariableDefinitions.Select(definition => definition.Variable).FirstOrDefault(candidate => candidate.Name == variable)
        ?? throw new ArgumentException($"The story has no variable called `${variable}`.", nameof(variable));

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
