using System.Collections.Immutable;

namespace Pibbles.Runtime;

/// <summary>
/// What the story asks the host to do next, from <see cref="DialogueRunner.Next"/>. Match on the concrete records;
/// later versions add kinds, so keep a default arm.
/// </summary>
public abstract record DialogueStep
{
    private protected DialogueStep()
    {
    }
}

/// <summary>Show a line and wait for the player.</summary>
/// <param name="Line">The line.</param>
public sealed record LineStep(Line Line) : DialogueStep;

/// <summary>Offer the player a choice. Pick with <see cref="DialogueRunner.Choose(ChoiceOption)"/> or by ID.</summary>
/// <param name="Options">Every option that is still on offer, in source order. At least one is available.</param>
public sealed record ChoiceStep(ImmutableArray<ChoiceOption> Options) : DialogueStep;

/// <summary>Carry out a command.</summary>
/// <param name="Command">The command and its arguments.</param>
/// <param name="Waits">Whether the story waits for the command to finish before asking for the next step.</param>
public sealed record CommandStep(CommandInvocation Command, bool Waits) : DialogueStep;

/// <summary>Change an actor's pose. Sent every time, even when the pose is the one the actor already has.</summary>
/// <param name="Actor">The actor's ID.</param>
/// <param name="Pose">The pose's name.</param>
public sealed record PoseStep(string Actor, string Pose) : DialogueStep;

/// <summary>Pause for a moment without showing anything.</summary>
/// <param name="Duration">How long.</param>
public sealed record WaitStep(TimeSpan Duration) : DialogueStep;

/// <summary>The dialogue is over.</summary>
public sealed record EndStep : DialogueStep;

/// <summary>One option of a choice.</summary>
/// <param name="Text">The option's text. Its <see cref="Line.Id"/> is the option's ID, and it has no speaker.</param>
/// <param name="IsAvailable">Whether the option's <c>@if</c> holds. The host decides whether to hide an unavailable option or grey it out.</param>
/// <param name="WasChosen">Whether the player has picked this option before.</param>
/// <param name="Number">The option's place among the choice's options as they're written in the source, starting at 1 and counting options <c>@once</c> has removed, so it's the same on every visit. It's for display and tools: pick an option with the option or its ID, never a number.</param>
public sealed record ChoiceOption(Line Text, bool IsAvailable, bool WasChosen, int Number)
{
    /// <summary>The option's ID.</summary>
    public string Id => Text.Id;
}
