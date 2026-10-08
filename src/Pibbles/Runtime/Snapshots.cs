using System.Text.Json.Serialization;
using Pibbles.Compiler;

namespace Pibbles.Runtime;

/// <summary>
/// Everything a <see cref="StoryState"/> holds, keyed so that it survives edits to the story. Take one with
/// <see cref="StoryState.CreateSnapshot"/>, store it with <see cref="SnapshotJson"/>, and load it with
/// <see cref="StoryState.Restore"/>.
/// </summary>
public sealed record StateSnapshot
{
    /// <summary>The format this version of Pibbles writes. It reads every format up to this one.</summary>
    public const int CurrentFormat = 1;

    /// <summary>The snapshot's format, which says what it holds. It's about Pibbles, not about the story.</summary>
    public required int Format { get; init; }

    /// <summary>The seed the host chose for the game.</summary>
    public required long Seed { get; init; }

    /// <summary>Every variable's value, by the variable's name without the <c>$</c>.</summary>
    public required IReadOnlyDictionary<string, SavedValue> Variables { get; init; }

    /// <summary>How many times each node has been visited, by the node's name when the snapshot was taken.</summary>
    public required IReadOnlyDictionary<string, int> Visits { get; init; }

    /// <summary>How many times each variation block has been entered, by the block's ID.</summary>
    public required IReadOnlyDictionary<string, int> BlockEntries { get; init; }

    /// <summary>The IDs of the options the player has chosen, for <c>WasChosen</c> and <c>@once</c>.</summary>
    public required IReadOnlyList<string> ChosenOptions { get; init; }

    /// <summary>Each actor's pose, by the actor's ID, for the actors the story has posed.</summary>
    public required IReadOnlyDictionary<string, string> Poses { get; init; }
}

/// <summary>
/// A variable's value in a <see cref="StateSnapshot"/>, exactly: one of <see cref="Bool"/>, <see cref="Number"/> and
/// <see cref="Text"/> is set, as <see cref="Type"/> says.
/// </summary>
public sealed record SavedValue
{
    /// <summary>
    /// The variable's type when the snapshot was taken, as the story writes it: <c>bool</c>, <c>number</c>, <c>string</c>,
    /// <c>duration</c>, <c>actor</c>, <c>node</c>, or an enum's name, which can't be any of those. It's
    /// <see cref="StoryType.ToString"/>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>The value of a <c>bool</c>.</summary>
    public bool? Bool { get; init; }

    /// <summary>The value of a <c>number</c>, or of a <c>duration</c> in seconds.</summary>
    public decimal? Number { get; init; }

    /// <summary>The value of a <c>string</c>, or the name of an enum member, an actor's ID or a node's name.</summary>
    public string? Text { get; init; }
}

/// <summary>
/// Where a <see cref="DialogueRunner"/> is, as IDs, never indexes, so that it survives edits to the story. Take one with
/// <see cref="DialogueRunner.CreateSnapshot"/> and load it with <see cref="DialogueRunner.Restore"/>. It's separate from
/// the <see cref="StateSnapshot"/>, because several runners can share one state; the host saves both.
/// </summary>
/// <remarks>
/// A dialogue is saved at a line or a choice, so at most one of <see cref="Line"/> and <see cref="Choice"/> is set.
/// When neither is, there's no dialogue in progress.
/// </remarks>
public sealed record RunnerSnapshot
{
    /// <summary>The format this version of Pibbles writes. It reads every format up to this one.</summary>
    public const int CurrentFormat = 1;

    /// <summary>A snapshot of a runner with no dialogue in progress.</summary>
    public static RunnerSnapshot Empty { get; } = new() { Format = CurrentFormat, Calls = [], Choice = [] };

    /// <summary>The snapshot's format, which says what it holds. It's about Pibbles, not about the story.</summary>
    public required int Format { get; init; }

    /// <summary>The IDs of the <c>@call</c>s the dialogue is inside, outermost first.</summary>
    public required IReadOnlyList<string> Calls { get; init; }

    /// <summary>The ID of the line the dialogue is showing, if it's at a line.</summary>
    public string? Line { get; init; }

    /// <summary>
    /// The IDs of the options of the choice the dialogue is waiting on, if it's at a choice: every option, in source order,
    /// including those that were hidden, unavailable or already used up. A choice has no ID of its own, so the whole set
    /// identifies it.
    /// </summary>
    public required IReadOnlyList<string> Choice { get; init; }

    /// <summary>Whether a dialogue is in progress: the snapshot is at a line or a choice.</summary>
    [JsonIgnore]
    public bool HasDialogue => Line is not null || Choice.Count > 0;
}

/// <summary>What taking or restoring a snapshot produced, and what it couldn't carry over.</summary>
/// <typeparam name="T">The snapshot, or the restored state or runner.</typeparam>
/// <param name="Value">The snapshot, or the restored state or runner.</param>
/// <param name="Problems">What was left out, dropped or done instead, in the order it was found. Empty when everything carried over.</param>
public sealed record SaveResult<T>(T Value, IReadOnlyList<SaveProblem> Problems);

/// <summary>Something a snapshot left out, or a restore couldn't carry over, and what was done instead.</summary>
/// <param name="Kind">What kind of problem.</param>
/// <param name="Subject">The saved ID or name the problem is about.</param>
/// <param name="Message">What happened and what was done instead, written for the game's developers.</param>
public sealed record SaveProblem(SaveProblemKind Kind, string Subject, string Message);

/// <summary>The kinds of <see cref="SaveProblem"/>. New kinds can be added, so a caller that switches on it keeps a default arm.</summary>
public enum SaveProblemKind
{
    /// <summary>A snapshot left out state or a position keyed by an ID the compiler made up, because that ID changes when the file is edited.</summary>
    FallbackId,

    /// <summary>The save has a variable the story no longer declares. Its value is dropped.</summary>
    UnknownVariable,

    /// <summary>A saved variable's type has changed. The variable keeps its starting value.</summary>
    VariableTypeChanged,

    /// <summary>A saved value or count no longer fits, such as a member of an enum that was removed. The variable keeps its starting value, or the count is dropped.</summary>
    InvalidValue,

    /// <summary>The save counts visits to a node that no longer exists under any name. The count is dropped.</summary>
    UnknownNode,

    /// <summary>Two saved node names now name the same node, through a <c>#was:</c> alias. Its visit count is their sum.</summary>
    VisitsMerged,

    /// <summary>The save counts entries to a variation block that no longer exists. The count is dropped.</summary>
    UnknownBlock,

    /// <summary>The save records a chosen option that no longer exists. It's dropped.</summary>
    UnknownOption,

    /// <summary>The save has a pose for an actor the story no longer declares. It's dropped.</summary>
    UnknownActor,

    /// <summary>The save has a pose the actor no longer has. The actor keeps its default pose.</summary>
    UnknownPose,

    /// <summary>
    /// A frame of the saved dialogue no longer exists: a line, every option of a choice, or a call. A lost call is dropped
    /// from the call stack. A lost line or choice resumes after the nearest call that survives, or, if none does, there's no
    /// dialogue in progress.
    /// </summary>
    LostFrame,

    /// <summary>The saved choice's options now belong to different choices. The dialogue resumes at the one holding the most of them.</summary>
    ChoiceSplit,

    /// <summary>The restored choice has no option available any more, so it's skipped like any other such choice.</summary>
    NoOptionAvailable,
}

/// <summary>
/// Checks snapshots before they're used. A snapshot can come from a corrupted or hand-edited save file, and JSON can hold
/// <see langword="null"/> where the types say it can't, which net8.0's serializer doesn't reject.
/// </summary>
internal static class SnapshotChecks
{
    /// <summary>Checks a snapshot before it's restored.</summary>
    /// <exception cref="ArgumentException">The format is newer than this version reads or isn't a format, or the snapshot has a null where a value must be.</exception>
    public static void Check(StateSnapshot snapshot, string parameter)
    {
        ArgumentNullException.ThrowIfNull(snapshot, parameter);
        Check(snapshot.Format, StateSnapshot.CurrentFormat, NullIn(snapshot), parameter);
    }

    /// <inheritdoc cref="Check(StateSnapshot, string)"/>
    public static void Check(RunnerSnapshot snapshot, string parameter)
    {
        ArgumentNullException.ThrowIfNull(snapshot, parameter);
        Check(snapshot.Format, RunnerSnapshot.CurrentFormat, NullIn(snapshot), parameter);
    }

    /// <summary>Where a state snapshot has a null that must be a value, as a JSON path, or <see langword="null"/> if it has none.</summary>
    public static string? NullIn(StateSnapshot snapshot)
    {
        if (snapshot.Variables is null)
            return "variables";

        foreach ((string name, SavedValue value) in snapshot.Variables)
        {
            if (value?.Type is null)
                return value is null ? $"variables.{name}" : $"variables.{name}.type";
        }

        if (snapshot.Visits is null)
            return "visits";

        if (snapshot.BlockEntries is null)
            return "blockEntries";

        if (NullIn(snapshot.ChosenOptions, "chosenOptions") is { } path)
            return path;

        if (snapshot.Poses is null)
            return "poses";

        return snapshot.Poses.FirstOrDefault(pose => pose.Value is null).Key is { } actor ? $"poses.{actor}" : null;
    }

    /// <summary>Where a runner snapshot has a null that must be a value, as a JSON path, or <see langword="null"/> if it has none.</summary>
    public static string? NullIn(RunnerSnapshot snapshot) => NullIn(snapshot.Calls, "calls") ?? NullIn(snapshot.Choice, "choice");

    private static string? NullIn(IReadOnlyList<string> ids, string name) =>
        ids is null ? name : ids.Select((id, index) => id is null ? $"{name}[{index}]" : null).FirstOrDefault(path => path is not null);

    private static void Check(int format, int current, string? nullPath, string parameter)
    {
        if (format > current)
            throw new ArgumentException($"The snapshot is in save format {format}, but this version of Pibbles reads formats up to {current}. It was probably saved by a newer version of the game.", parameter);

        if (format < 1)
            throw new ArgumentException($"The snapshot's format is {format}, which isn't a save format. Formats start at 1.", parameter);

        if (nullPath is not null)
            throw new ArgumentException($"The snapshot has null at `{nullPath}`, where it needs a value. The save is probably corrupted.", parameter);
    }
}
