using System.Collections.Immutable;
using Pibbles.Compiler;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>How a <see cref="DialogueRunner"/> behaves.</summary>
public sealed record RunnerOptions
{
    /// <summary>
    /// How many instructions <see cref="DialogueRunner.Next"/> runs without producing a step before it gives up, reports an
    /// <see cref="RuntimeWarningKind.InfiniteLoop"/> warning and ends the dialogue, so a story can never hang the game. Defaults to 100,000.
    /// </summary>
    public int InstructionBudget { get; init; } = 100_000;

    /// <summary>
    /// Receives each <see cref="RuntimeWarning"/>, after the instruction that raised it has finished. Nothing about the
    /// story's flow depends on it, and without one, warnings are dropped.
    /// </summary>
    public Action<RuntimeWarning>? OnWarning { get; init; }
}

/// <summary>
/// Runs a story one step at a time: the host calls <see cref="Next"/>, does what the step says, and calls it again. It's
/// pull-based, synchronous and single-threaded, with no timers, events or skip mode.
/// </summary>
/// <remarks>
/// <para>
/// Several runners can share one <see cref="StoryState"/>, each with its own place in the story.
/// </para>
/// <para>
/// An instruction that throws, such as a host function failing in a condition, changes nothing, and neither does the
/// runner's position: calling <see cref="Next"/> again tries it again. Instructions that finished before it in the same
/// call stay finished. Warnings an instruction raised before it threw are dropped.
/// </para>
/// <para>
/// Misuse throws <see cref="InvalidOperationException"/>: <see cref="Next"/> before <see cref="Start"/> or while a choice is
/// waiting, <see cref="Choose(string)"/> with no choice waiting or with an option that isn't offered or isn't available,
/// and <see cref="Start"/> with an unknown node. Story content never throws.
/// </para>
/// </remarks>
public sealed class DialogueRunner
{
    private static readonly EndStep End = new();

    private readonly Story story;
    private readonly StoryState state;
    private readonly HostFunctions functions;
    private readonly RunnerOptions options;
    private readonly Context context;
    private readonly List<RuntimeWarning> pending = [];
    private readonly Stack<Frame> frames = new();

    private Phase phase = Phase.NotStarted;
    private CompiledNode? node;
    private int index;
    private List<(CompiledOption Compiled, ChoiceOption Offered)> offered = [];

    /// <summary>Creates a runner that isn't running anything yet. Call <see cref="Start"/>.</summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="state">The state to run against, which must be for the same story.</param>
    /// <param name="functions">The host functions the story calls.</param>
    /// <param name="options">How the runner behaves, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentException">The state is for another story.</exception>
    public DialogueRunner(Story story, StoryState state, HostFunctions functions, RunnerOptions? options = null)
    {
        if (!ReferenceEquals(state.Story, story))
            throw new ArgumentException("The state belongs to another story.", nameof(state));

        this.story = story;
        this.state = state;
        this.functions = functions;
        this.options = options ?? new();
        context = new(this);
    }

    private enum Phase
    {
        NotStarted,
        Running,
        AwaitingChoice,
        Ended,
    }

    /// <summary>
    /// Starts a dialogue at a node, dropping any dialogue in progress, and counts a visit to the node. A node's old
    /// <c>#was:</c> names work too.
    /// </summary>
    /// <param name="node">The node's current name, or an old one.</param>
    /// <exception cref="InvalidOperationException">The story has no such node. The message suggests the closest name.</exception>
    public void Start(string node)
    {
        string name = story.Nodes.ContainsKey(node) ? node
            : story.Aliases.GetValueOrDefault(node)
            ?? throw new InvalidOperationException($"The story has no node called `{node}`.{Suggestion(node)}");

        frames.Clear();
        offered = [];
        pending.Clear();
        Enter(story.Nodes[name]);
        phase = Phase.Running;
    }

    /// <summary>
    /// Runs the story until it has something for the host to do. After <see cref="EndStep"/>, it returns <see cref="EndStep"/>
    /// again until <see cref="Start"/> is called.
    /// </summary>
    /// <exception cref="InvalidOperationException">No dialogue has been started, or a choice is waiting to be answered.</exception>
    public DialogueStep Next()
    {
        switch (phase)
        {
            case Phase.NotStarted:
                throw new InvalidOperationException("Call Start before Next.");
            case Phase.AwaitingChoice:
                throw new InvalidOperationException("A choice is waiting. Call Choose before Next.");
            case Phase.Ended:
                return End;
        }

        for (int executed = 0; executed < options.InstructionBudget; executed++)
        {
            DialogueStep? step;
            try
            {
                step = Execute(node!.Instructions[index]);
            }
            catch
            {
                pending.Clear();
                throw;
            }

            Flush();
            if (step is not null)
                return step;
        }

        Warn(new(RuntimeWarningKind.InfiniteLoop, $"This story went round in a loop for {options.InstructionBudget} steps without showing anything, so I ended the dialogue.", node!.Location));
        Flush();
        return Finish();
    }

    /// <summary>Picks an option of the choice that is waiting.</summary>
    /// <param name="option">An option from the current <see cref="ChoiceStep"/>.</param>
    /// <inheritdoc cref="Choose(string)"/>
    public void Choose(ChoiceOption option) => Choose(option.Id);

    /// <summary>
    /// Picks an option of the choice that is waiting. The option counts as chosen from now on, before its body runs; the
    /// next <see cref="Next"/> continues in its body.
    /// </summary>
    /// <param name="id">The option's ID.</param>
    /// <exception cref="InvalidOperationException">No choice is waiting, or the option isn't in it or isn't available.</exception>
    public void Choose(string id)
    {
        if (phase is not Phase.AwaitingChoice)
            throw new InvalidOperationException("There's no choice waiting.");

        (CompiledOption Compiled, ChoiceOption Offered) match = offered.Find(candidate => candidate.Offered.Id == id);
        if (match.Compiled is null)
            throw new InvalidOperationException($"`{id}` isn't one of the options on offer.");

        if (!match.Offered.IsAvailable)
            throw new InvalidOperationException($"The option `{id}` isn't available.");

        state.ChosenOptions.Add(id);
        index = match.Compiled.Body;
        offered = [];
        phase = Phase.Running;
    }

    private DialogueStep? Execute(Instruction instruction)
    {
        switch (instruction)
        {
            case LineInstruction line:
                Line shown = BuildLine(line.Id);
                index++;
                return new LineStep(shown);

            case PoseInstruction pose:
                state.Poses[pose.Actor.Name] = pose.Pose.Name;
                index++;
                return new PoseStep(pose.Actor.Name, pose.Pose.Name);

            case ChoiceInstruction choice:
                return ExecuteChoice(choice);

            case BranchInstruction branch:
                index = branch.Target;
                return null;

            case BranchIfFalseInstruction branch:
                index = Evaluator.Evaluate(branch.Condition, context).AsBool ? index + 1 : branch.Target;
                return null;

            case SetInstruction set:
                state.Variables[set.Variable] = Evaluator.Evaluate(set.Value, context);
                index++;
                return null;

            case WaitInstruction wait:
                return ExecuteWait(wait);

            case CommandInstruction command:
                Value[] arguments = [.. command.Arguments.Select(argument => Evaluator.Evaluate(argument, context))];
                index++;
                return new CommandStep(new(command.Command, arguments, command.Location, EmitNow), command.Waits);

            case JumpInstruction jump:
                Enter(story.Nodes[jump.Node]);
                return null;

            case CallInstruction call:
                frames.Push(new(node!, index + 1, call.Id));
                Enter(story.Nodes[call.Node]);
                return null;

            case ReturnInstruction when frames.Count > 0:
                Frame frame = frames.Pop();
                node = frame.Node;
                index = frame.ReturnIndex;
                return null;

            case ReturnInstruction or EndInstruction:
                return Finish();

            case VariationInstruction variation:
                int entries = state.BlockEntries.GetValueOrDefault(variation.BlockId);
                int count = variation.Alternatives.Count;
                int pick = variation.Kind switch
                {
                    BlockKind.Sequence => Math.Min(entries, count - 1),
                    BlockKind.Cycle => entries % count,
                    _ => entries == 0 ? 0 : -1,
                };
                state.BlockEntries[variation.BlockId] = entries + 1;
                index = pick < 0 ? variation.Exit : variation.Alternatives[pick];
                return null;

            default:
                throw new NotSupportedException(instruction.GetType().Name);
        }
    }

    private ChoiceStep? ExecuteChoice(ChoiceInstruction choice)
    {
        List<(CompiledOption Compiled, ChoiceOption Offered)> delivered = [];
        foreach (CompiledOption option in choice.Options)
        {
            bool wasChosen = state.ChosenOptions.Contains(option.Id);
            if (option.IsOnce && wasChosen)
                continue;

            bool available = option.Condition is null || Evaluator.Evaluate(option.Condition, context).AsBool;
            delivered.Add((option, new ChoiceOption(BuildLine(option.Id), available, wasChosen)));
        }

        if (!delivered.Any(option => option.Offered.IsAvailable))
        {
            index = choice.Join;
            return null;
        }

        offered = delivered;
        phase = Phase.AwaitingChoice;
        return new ChoiceStep([.. delivered.Select(option => option.Offered)]);
    }

    private WaitStep? ExecuteWait(WaitInstruction wait)
    {
        decimal seconds = Evaluator.Evaluate(wait.Duration, context).AsDecimal;
        if (seconds <= 0)
        {
            Warn(new(RuntimeWarningKind.NonPositiveWait, "This wait isn't for more than zero time, so I skipped it.", wait.Location));
            index++;
            return null;
        }

        TimeSpan duration = Durations.ToTimeSpan(seconds, wait.Location, Warn);
        index++;
        return new WaitStep(duration);
    }

    private Line BuildLine(string id)
    {
        Template template = story.Templates[id];
        string text = PlainText.Render(template.Content, context);
        return new(id, template.Speaker?.Name, template.Speaker?.DisplayName, text, [], [], [], []);
    }

    /// <summary>Moves to the start of a node, counting the visit.</summary>
    private void Enter(CompiledNode target)
    {
        node = target;
        index = 0;
        state.Visits[target.Name] = state.Visits.GetValueOrDefault(target.Name) + 1;
    }

    private EndStep Finish()
    {
        frames.Clear();
        offered = [];
        node = null;
        phase = Phase.Ended;
        return End;
    }

    private string Suggestion(string name) =>
        Suggestions.Closest(name, story.Nodes.Keys.Concat(story.Aliases.Keys)) is { } closest ? $" Did you mean `{closest}`?" : "";

    private void Warn(RuntimeWarning warning) => pending.Add(warning);

    /// <summary>Reports a warning raised after its instruction has finished, such as one from a command's duration the host reads.</summary>
    private void EmitNow(RuntimeWarning warning) => options.OnWarning?.Invoke(warning);

    private void Flush()
    {
        if (pending.Count == 0)
            return;

        RuntimeWarning[] warnings = [.. pending];
        pending.Clear();
        if (options.OnWarning is { } handler)
            Array.ForEach(warnings, handler.Invoke);
    }

    private readonly record struct Frame(CompiledNode Node, int ReturnIndex, string CallId);

    private sealed class Context(DialogueRunner runner) : IEvaluationContext
    {
        public Value GetVariable(VariableSymbol variable) => runner.state.Variables[variable];

        public int GetVisits(string node) => runner.state.Visits.GetValueOrDefault(node);

        public Value CallFunction(CallExpr call, IReadOnlyList<Value> arguments) =>
            runner.functions.Invoke(runner.story, call, arguments, runner.Warn);

        public void Warn(RuntimeWarning warning) => runner.Warn(warning);
    }
}
