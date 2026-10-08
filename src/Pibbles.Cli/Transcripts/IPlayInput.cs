using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Cli.Transcripts;

/// <summary>
/// What drives a <see cref="TranscriptPlayer"/> run beyond its script: answers to the choices the script has no answer for,
/// values for the calls no stub covers, and whether to carry on after each step. A person at a terminal is one; a test's
/// random walk is another.
/// </summary>
internal interface IPlayInput
{
    /// <summary>Whether <see cref="Stub"/> can supply values. When it can't, a declared function without a stub fails the run at its start, as in a script.</summary>
    bool SuppliesStubs { get; }

    /// <summary>Picks an available option of a choice the script has no answer for, or returns <see langword="null"/> to stop the run.</summary>
    ChoiceOption? Choose(ChoiceStep choice);

    /// <summary>
    /// The value a call returns when no stub covers it, as a host value of the function's return type, or
    /// <see langword="null"/> to stop the run. The player remembers it for the rest of the run.
    /// </summary>
    object? Stub(FunctionInfo function, IReadOnlyList<object?> arguments);

    /// <summary>Called after each step is printed. Returns <see langword="false"/> to stop the run.</summary>
    bool Continue(DialogueStep step);
}

/// <summary>How a test walks a story at random: it picks each answer the script lacks, and stops after a number of steps, since a random walk may never end.</summary>
/// <param name="choose">Picks an available option of a choice the script has no answer for.</param>
/// <param name="maxSteps">How many steps to play before stopping.</param>
internal sealed class TranscriptWalk(Func<ChoiceStep, ChoiceOption> choose, int maxSteps) : IPlayInput
{
    private int steps;

    public bool SuppliesStubs => false;

    public ChoiceOption? Choose(ChoiceStep choice) => choose(choice);

    public object? Stub(FunctionInfo function, IReadOnlyList<object?> arguments) => null;

    public bool Continue(DialogueStep step) => ++steps < maxSteps;
}
