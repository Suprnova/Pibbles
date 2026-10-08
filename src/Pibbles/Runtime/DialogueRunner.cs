using Pibbles.Compiler;
using Pibbles.Semantics;
using System.Collections.Immutable;

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
/// pull-based, synchronous and single-threaded, with no timers or events. Skip mode belongs to the host, except for
/// <see cref="FastForward"/>, which skips to the next save point.
/// </summary>
/// <remarks>
/// <para>
/// Several runners can share one <see cref="StoryState"/>, each with its own place in the story. A runner's place is saved
/// with <see cref="CreateSnapshot"/> and loaded with <see cref="Restore"/>, separately from the state.
/// </para>
/// <para>
/// An instruction that throws, such as a host function failing in a condition, changes nothing, and neither does the
/// runner's position: calling <see cref="Next"/> again tries it again. Instructions that finished before it in the same
/// call stay finished. Warnings an instruction raised before it threw are dropped.
/// </para>
/// <para>
/// Misuse throws <see cref="InvalidOperationException"/>: <see cref="Next"/> before <see cref="Start"/> or while a choice is
/// waiting, <see cref="Choose(string)"/> with no choice waiting or with an option that isn't offered or isn't available,
/// <see cref="Start"/> with an unknown node, and <see cref="CreateSnapshot"/> away from a save point. Story content never throws.
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
    private string? shownLine;
    private bool restored;

    /// <summary>Creates a runner that isn't running anything yet. Call <see cref="Start"/>.</summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="state">The state to run against, which must be for the same story.</param>
    /// <param name="functions">The host functions the story calls.</param>
    /// <param name="options">How the runner behaves, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentException">The state is for another story.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The instruction budget isn't positive.</exception>
    public DialogueRunner(Story story, StoryState state, HostFunctions functions, RunnerOptions? options = null)
    {
        if (!ReferenceEquals(state.Story, story))
            throw new ArgumentException("The state belongs to another story.", nameof(state));

        this.options = options ?? new();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.options.InstructionBudget, nameof(options));
        this.story = story;
        this.state = state;
        this.functions = functions;
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
        string name = story.CompiledNodes.ContainsKey(node) ? node
            : story.Aliases.GetValueOrDefault(node)
            ?? throw new InvalidOperationException($"The story has no node called `{node}`.{Suggestion(node)}");

        frames.Clear();
        offered = [];
        pending.Clear();
        shownLine = null;
        restored = false;
        Enter(story.CompiledNodes[name]);
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

        int budget = options.InstructionBudget;
        return Step(ref budget);
    }

    /// <summary>
    /// Finishes whatever the dialogue is doing and runs on to the next save point: the next line or choice, or the end. A
    /// host calls it when the player saves at any other moment, then calls <see cref="CreateSnapshot"/>. It's skip mode
    /// that stops at the first line: the host carries out the commands it passed instantly (with a skip variant of their
    /// effect), applies the poses, drops the waits, and shows the final line or choice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The instruction budget covers the whole fast-forward, so a story that loops forever giving commands still can't hang
    /// the game: the runner reports an <see cref="RuntimeWarningKind.InfiniteLoop"/> warning and ends the dialogue.
    /// </para>
    /// <para>
    /// If an instruction throws partway, such as a host function failing in a condition, the steps already passed have run:
    /// their <c>@set</c>s happened and the runner has moved on. So the exception is a <see cref="FastForwardException"/>
    /// whose <see cref="FastForwardException.Passed"/> holds them, for the host to carry out, with the original exception
    /// as its <see cref="Exception.InnerException"/>. As with <see cref="Next"/>, the instruction that threw changed
    /// nothing, and calling <see cref="FastForward"/> again tries it again.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Every step it passed, in order, ending with the <see cref="LineStep"/>, <see cref="ChoiceStep"/> or
    /// <see cref="EndStep"/> it stopped at. It's empty when the runner is already at a save point: right after a
    /// <see cref="LineStep"/>, while a choice is waiting, right after <see cref="Restore"/> landed on a line or a choice, or
    /// with no dialogue in progress.
    /// </returns>
    /// <exception cref="FastForwardException">An instruction threw. The exception holds the steps passed before it.</exception>
    public IReadOnlyList<DialogueStep> FastForward()
    {
        if (AtSavePoint)
            return [];

        List<DialogueStep> passed = [];
        int budget = options.InstructionBudget;
        try
        {
            do
            {
                passed.Add(Step(ref budget));
            }
            while (passed[^1] is not (LineStep or ChoiceStep or EndStep));
        }
        catch (Exception exception)
        {
            throw new FastForwardException(passed, exception);
        }

        return passed;
    }

    /// <summary>
    /// Takes a snapshot of where the dialogue is, as IDs: the line the host is showing or the options of the choice that is
    /// waiting, and the <c>@call</c>s it's inside. It can be taken right after <see cref="Next"/> returned a
    /// <see cref="LineStep"/>, while a choice is waiting, right after <see cref="Restore"/> landed on a line or a choice, or
    /// with no dialogue in progress (never started, or ended), which gives <see cref="RunnerSnapshot.Empty"/>. At any other
    /// moment, call <see cref="FastForward"/> first.
    /// </summary>
    /// <returns>
    /// The snapshot, and a <see cref="SaveProblemKind.FallbackId"/> problem for each ID it left out because the compiler made
    /// it up. A dialogue waiting on a line or call with such an ID, or on a choice whose options all have one, is saved as no
    /// dialogue in progress.
    /// </returns>
    /// <exception cref="InvalidOperationException">The runner isn't at a line or a choice, so there's no save point.</exception>
    public SaveResult<RunnerSnapshot> CreateSnapshot()
    {
        if (phase is Phase.NotStarted or Phase.Ended)
            return new(RunnerSnapshot.Empty, []);

        if (!AtSavePoint)
            throw new InvalidOperationException("The dialogue isn't at a line or a choice, so it can't be saved here. Call FastForward first, then CreateSnapshot.");

        Instruction? waiting = phase is Phase.AwaitingChoice || restored ? node!.Instructions[index] : null;
        string? line = shownLine ?? (waiting as LineInstruction)?.Id;
        string[] calls = [.. frames.Reverse().Select(frame => frame.CallId)];
        string[] choice = waiting is ChoiceInstruction instruction ? [.. instruction.Options.Select(option => option.Id)] : [];
        string[] kept = [.. choice.Where(id => !story.FallbackIds.Contains(id))];
        bool lost = calls.Any(story.FallbackIds.Contains) || (line is not null && story.FallbackIds.Contains(line)) || (choice.Length > 0 && kept.Length == 0);

        string outcome = lost ? "so the dialogue is saved as not in progress" : "so the snapshot leaves it out";
        SaveProblem[] problems =
        [
            .. calls.Append(line).Concat(choice).OfType<string>().Where(story.FallbackIds.Contains)
                .Select(id => new SaveProblem(SaveProblemKind.FallbackId, id, $"`{id}` has no `#id`, {outcome}. Run `pibbles ids` to give it one.")),
        ];
        return new(lost ? RunnerSnapshot.Empty : new() { Format = RunnerSnapshot.CurrentFormat, Calls = calls, Line = line, Choice = kept }, problems);
    }

    /// <summary>
    /// Restores a runner from a snapshot, against the story as it is now, which may have changed since the snapshot was
    /// taken. Then call <see cref="Next"/> as usual: it shows the saved line again, rendered afresh, or offers the saved
    /// choice again, its conditions evaluated against the state as it is now. Restoring never counts a visit, and never
    /// replays the pose before a line: the host redraws poses from <see cref="StoryState.GetPose"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every saved ID is looked up exactly, never guessed, wherever it has moved. A choice is found by any of its saved
    /// option IDs that is still an option. If they now belong to different choices, it resumes at the one that holds the
    /// most of them, the earliest in saved order on a tie, and reports the split. If no option is available any more, the
    /// choice is skipped like any other such choice, and reported.
    /// </para>
    /// <para>
    /// A frame whose ID no longer exists is lost, and reported. A lost call is dropped from the call stack. A lost line or
    /// choice resumes as if its node had returned to the nearest call that survives, at the instruction after it. If no
    /// frame survives, there's no dialogue in progress: the runner is ended, and <see cref="Next"/> returns
    /// <see cref="EndStep"/> until <see cref="Start"/>.
    /// </para>
    /// <para>
    /// A runner restored at a line or a choice is at a save point until <see cref="Next"/> shows it again, so a game can
    /// save straight after loading. One that resumed after a call, or skipped a choice with nothing available, isn't.
    /// </para>
    /// </remarks>
    /// <param name="story">The story as it is now.</param>
    /// <param name="state">The state to run against, usually restored with <see cref="StoryState.Restore"/>. It must be for the same story.</param>
    /// <param name="functions">The host functions the story calls.</param>
    /// <param name="snapshot">The snapshot, in any format up to <see cref="RunnerSnapshot.CurrentFormat"/>.</param>
    /// <param name="options">How the runner behaves, or <see langword="null"/> for the defaults.</param>
    /// <returns>The restored runner, and the problems: what was lost, and what was done instead.</returns>
    /// <exception cref="ArgumentException">The snapshot's format is newer than this version of Pibbles reads, or the state is for another story.</exception>
    /// <exception cref="HostFunctionException">A host function failed while a restored choice checked its options.</exception>
    public static SaveResult<DialogueRunner> Restore(Story story, StoryState state, HostFunctions functions, RunnerSnapshot snapshot, RunnerOptions? options = null)
    {
        SnapshotChecks.Check(snapshot, nameof(snapshot));
        var runner = new DialogueRunner(story, state, functions, options);
        List<SaveProblem> problems = [];
        runner.Resume(snapshot, problems);
        return new(runner, problems);
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

        (CompiledOption Compiled, ChoiceOption Offered) = offered.Find(candidate => candidate.Offered.Id == id);
        if (Compiled is null)
            throw new InvalidOperationException($"`{id}` isn't one of the options on offer.");

        if (!Offered.IsAvailable)
            throw new InvalidOperationException($"The option `{id}` isn't available.");

        state.ChosenOptions.Add(id);
        index = Compiled.Body;
        offered = [];
        phase = Phase.Running;
    }

    /// <summary>Runs instructions until one gives a step, spending <paramref name="budget"/>, and ends the dialogue with a warning when it runs out.</summary>
    private DialogueStep Step(ref int budget)
    {
        shownLine = null;
        for (; budget > 0; budget--)
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

            restored = false;
            Flush();
            if (step is not null)
            {
                budget--;
                shownLine = (step as LineStep)?.Line.Id;
                return step;
            }
        }

        Warn(new(RuntimeWarningKind.InfiniteLoop, $"This story went round in a loop for {options.InstructionBudget} steps without showing anything, so I ended the dialogue.", node!.Location));
        Flush();
        return Finish();
    }

    private void Resume(RunnerSnapshot snapshot, List<SaveProblem> problems)
    {
        if (!snapshot.HasDialogue)
        {
            Finish();
            return;
        }

        foreach (string id in snapshot.Calls)
        {
            if (story.Locate(id) is { } site && site.Node.Instructions[site.Index] is CallInstruction)
                frames.Push(new(site.Node, site.Index + 1, id));
            else
                problems.Add(Lost(id, $"The saved dialogue is inside the call `{id}`, which no longer exists, so it won't return there."));
        }

        bool resumed = snapshot.Line is { } line ? ResumeLine(line, problems) : ResumeChoice(snapshot.Choice, problems);
        if (!resumed)
        {
            if (!frames.TryPop(out Frame frame))
            {
                Finish();
                return;
            }

            node = frame.Node;
            index = frame.ReturnIndex;
        }

        phase = Phase.Running;
    }

    private bool ResumeLine(string id, List<SaveProblem> problems)
    {
        if (story.Locate(id) is not { } site || site.Node.Instructions[site.Index] is not LineInstruction)
        {
            problems.Add(Lost(id, $"The saved line `{id}` no longer exists, so the dialogue {Unwinding()}."));
            return false;
        }

        (node, index) = site;
        restored = true;
        return true;
    }

    private bool ResumeChoice(IReadOnlyList<string> ids, List<SaveProblem> problems)
    {
        var choices = ids
            .Select(id => (Id: id, Site: story.Locate(id)))
            .Where(option => option.Site is { } site && site.Node.Instructions[site.Index] is ChoiceInstruction)
            .GroupBy(option => option.Site!.Value)
            .OrderByDescending(group => group.Count())
            .ToList();
        string saved = string.Join(", ", ids.Select(id => $"`{id}`"));
        if (choices.Count == 0)
        {
            problems.Add(Lost(ids[0], $"None of the saved choice's options ({saved}) exists any more, so the dialogue {Unwinding()}."));
            return false;
        }

        (node, index) = choices[0].Key;
        string first = choices[0].First().Id;
        if (choices.Count > 1)
            problems.Add(new(SaveProblemKind.ChoiceSplit, first, $"The saved choice's options ({saved}) now belong to {choices.Count} different choices, so the dialogue resumes at the one with {string.Join(", ", choices[0].Select(option => $"`{option.Id}`"))}."));

        var choice = (ChoiceInstruction)node.Instructions[index];
        if (Offer(choice).Any(option => option.Offered.IsAvailable))
        {
            pending.Clear();
            restored = true;
            return true;
        }

        Flush();
        problems.Add(new(SaveProblemKind.NoOptionAvailable, first, $"None of the options of the saved choice with `{first}` is available now, so it's skipped."));
        index = choice.Join;
        return true;
    }

    /// <summary>
    /// Whether a snapshot can be taken now: with no dialogue in progress, right after a line, while a choice waits, or right
    /// after <see cref="Restore"/> landed on a line or a choice that hasn't been shown again yet.
    /// </summary>
    private bool AtSavePoint => phase is not Phase.Running || shownLine is not null || restored;

    private string Unwinding() => frames.Count > 0 ? "resumes after the call it was inside" : "is no longer in progress";

    private static SaveProblem Lost(string id, string message) => new(SaveProblemKind.LostFrame, id, message);

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
                Enter(story.CompiledNodes[jump.Node]);
                return null;

            case CallInstruction call:
                frames.Push(new(node!, index + 1, call.Id));
                Enter(story.CompiledNodes[call.Node]);
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
        List<(CompiledOption Compiled, ChoiceOption Offered)> delivered = Offer(choice);
        if (!delivered.Any(option => option.Offered.IsAvailable))
        {
            index = choice.Join;
            return null;
        }

        offered = delivered;
        phase = Phase.AwaitingChoice;
        return new ChoiceStep([.. delivered.Select(option => option.Offered)]);
    }

    /// <summary>The options still on offer, with their text and whether each is available now.</summary>
    private List<(CompiledOption Compiled, ChoiceOption Offered)> Offer(ChoiceInstruction choice)
    {
        List<(CompiledOption Compiled, ChoiceOption Offered)> delivered = [];
        foreach ((CompiledOption option, int index) in choice.Options.Select((option, index) => (option, index)))
        {
            bool wasChosen = state.ChosenOptions.Contains(option.Id);
            if (option.IsOnce && wasChosen)
                continue;

            bool available = option.Condition is null || Evaluator.Evaluate(option.Condition, context).AsBool;
            delivered.Add((option, new ChoiceOption(BuildLine(option.Id), available, wasChosen, index + 1)));
        }

        return delivered;
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
        return LineRenderer.Render(id, story.Templates[id], story, context, Warn, EmitNow);
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
        shownLine = null;
        restored = false;
        node = null;
        phase = Phase.Ended;
        return End;
    }

    private string Suggestion(string name) =>
        Suggestions.Closest(name, story.CompiledNodes.Keys.Concat(story.Aliases.Keys)) is { } closest ? $" Did you mean `{closest}`?" : "";

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

/// <summary>
/// An instruction threw during <see cref="DialogueRunner.FastForward"/>. The steps passed before it have already run, so
/// the host must still carry them out, or the stage and the story go out of step.
/// </summary>
public sealed class FastForwardException : Exception
{
    internal FastForwardException(IReadOnlyList<DialogueStep> passed, Exception inner)
        : base($"Fast-forwarding stopped after {passed.Count} step{(passed.Count == 1 ? "" : "s")}: {inner.Message} Carry out the steps in {nameof(Passed)} before handling the error.", inner)
    {
        Passed = passed;
    }

    /// <summary>The steps passed before the instruction threw, in order. They have run, and the host carries them out as it would have.</summary>
    public IReadOnlyList<DialogueStep> Passed { get; }
}
