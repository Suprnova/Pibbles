using CsCheck;
using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Tests.Runtime;

namespace Pibbles.Tests.Properties;

/// <summary>
/// The save-anywhere property: play a random walk, ask to save at a random step (fast-forwarding to the next save point
/// if it isn't one), round-trip both snapshots through JSON, restore them into a fresh state and runner, and finish the
/// walk with the same remaining choices. The run must match the same walk played without a save, with one exception:
/// </summary>
/// <remarks>
/// <para>
/// <b>The exception.</b> When the save lands on a line or a choice, that step's entry appears twice in a row: once before
/// the save, and once when the restored runner shows it again. The choice is answered only after the second. When the
/// fast-forward reaches the end instead, there's nothing to show again and the runs are identical.
/// </para>
/// <para>
/// A walk may not end, so both runs stop after a fixed number of entries, and the comparison is over the entries both
/// have. When both reach the end, their final states must also be the same, which shows that restoring counted no visit.
/// A save whose snapshots leave something out because of a missing <c>#id</c> can't promise the same run, so such a case
/// only checks that every problem is of that kind.
/// </para>
/// </remarks>
public class SaveAnywhereTests
{
    private const int MaxEntries = 300;

    [Fact]
    public void Restore_SavedAtAnyStep_FinishesLikeAnUninterruptedRun() =>
        PropertyCheck.Run(Gen.Select(Walk.Gen, Gen.Int[0, 1000], (walk, save) => (Walk: walk, Save: save)), Check, iterations: 400, print: input => $"{input.Walk}, save {input.Save}");

    /// <summary>Saves at a step the walk reaches: step <c>1 + Save mod n</c>, where <c>n</c> is the number of steps the uninterrupted run takes within the limit.</summary>
    private static void Check((Walk Walk, int Save) input)
    {
        Run uninterrupted = Play(input.Walk, saveAt: null, MaxEntries * 2);
        int steps = uninterrupted.Entries.Take(MaxEntries).Count(entry => !entry.StartsWith("Chose ", StringComparison.Ordinal));
        Run saved = Play(input.Walk, 1 + (input.Save % steps), MaxEntries);
        if (saved.Problems.Count > 0)
        {
            Assert.All(saved.Problems, problem => Assert.Equal(SaveProblemKind.FallbackId, problem.Kind));
            return;
        }

        List<string> expected = [.. uninterrupted.Entries];
        if (saved.Replayed is int replayed && replayed < expected.Count)
            expected.Insert(replayed + 1, expected[replayed]);

        int common = Math.Min(expected.Count, saved.Entries.Count);
        Assert.Equal(expected.Take(common), saved.Entries.Take(common));
        if (saved.Entries[^1] is "End")
        {
            Assert.Equal(expected.Count, saved.Entries.Count);
            Assert.Equal(uninterrupted.FinalState, saved.FinalState);
        }
    }

    /// <summary>Plays a walk, saving and restoring once at step <paramref name="saveAt"/> if it's set.</summary>
    private static Run Play(Walk walk, int? saveAt, int maxEntries)
    {
        Story story = walk.Compiled;
        HostFunctions functions = Stubs(walk);
        Func<ChoiceStep, ChoiceOption> choose = walk.Chooser();
        var state = new StoryState(story, 7);
        var runner = new DialogueRunner(story, state, functions);
        runner.Start(walk.Node);

        List<string> entries = [];
        List<SaveProblem> problems = [];
        int? replayed = null;
        for (int steps = 1; entries.Count < maxEntries; steps++)
        {
            DialogueStep step = runner.Next();
            entries.Add(Game.Format(step));
            if (steps == saveAt)
            {
                foreach (DialogueStep passed in runner.FastForward())
                {
                    entries.Add(Game.Format(passed));
                    step = passed;
                }

                (state, runner) = SaveAndRestore(story, functions, state, runner, problems);
                if (problems.Count > 0 || step is EndStep)
                    break;

                replayed = entries.Count - 1;
                continue;
            }

            if (step is EndStep)
                break;

            if (step is ChoiceStep choice)
            {
                ChoiceOption picked = choose(choice);
                runner.Choose(picked);
                entries.Add($"Chose {picked.Id}");
            }
        }

        return new(entries, replayed, SnapshotJson.Serialize(state.CreateSnapshot().Value), problems);
    }

    private static (StoryState, DialogueRunner) SaveAndRestore(Story story, HostFunctions functions, StoryState state, DialogueRunner runner, List<SaveProblem> problems)
    {
        (StateSnapshot stateSnapshot, IReadOnlyList<SaveProblem> stateProblems) = state.CreateSnapshot();
        (RunnerSnapshot runnerSnapshot, IReadOnlyList<SaveProblem> runnerProblems) = runner.CreateSnapshot();
        problems.AddRange([.. stateProblems, .. runnerProblems]);

        (StoryState restoredState, IReadOnlyList<SaveProblem> restoreProblems) = StoryState.Restore(story, SnapshotJson.DeserializeState(SnapshotJson.Serialize(stateSnapshot)));
        (DialogueRunner restoredRunner, IReadOnlyList<SaveProblem> runnerRestoreProblems) = DialogueRunner.Restore(story, restoredState, functions, SnapshotJson.DeserializeRunner(SnapshotJson.Serialize(runnerSnapshot)));
        Assert.Empty(restoreProblems);
        Assert.Empty(runnerRestoreProblems);
        return (restoredState, restoredRunner);
    }

    private static HostFunctions Stubs(Walk walk)
    {
        var functions = new HostFunctions();
        foreach ((FunctionInfo function, object value) in walk.StubValues)
            functions.AddDynamic(function.Name, [.. function.Parameters.Select(parameter => parameter.Type.HostType)], function.ReturnType.HostType, _ => value);

        return functions;
    }

    /// <param name="Entries">Every step, formatted, and every answer, as <c>Chose id</c>.</param>
    /// <param name="Replayed">The index of the entry the restored runner showed again, if it did.</param>
    /// <param name="FinalState">The state snapshot at the end, as JSON.</param>
    /// <param name="Problems">What saving left out.</param>
    private sealed record Run(List<string> Entries, int? Replayed, string FinalState, List<SaveProblem> Problems);
}
