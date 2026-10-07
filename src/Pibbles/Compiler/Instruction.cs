using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Compiler;

/// <summary>
/// One step of a node. A node compiles to a flat list of these, so a position in the story is a node and an index.
/// A <c>Branch</c> target is an index in the same node; a <c>Jump</c> or <c>Call</c> target is another node.
/// </summary>
internal abstract record Instruction;

/// <summary>Shows a line. Its <see cref="Template"/> is in the story's templates under <paramref name="Id"/>.</summary>
internal sealed record LineInstruction(string Id) : Instruction;

/// <summary>Changes an actor's pose. A posed line gets one before its <see cref="LineInstruction"/>, every time.</summary>
internal sealed record PoseInstruction(ActorSymbol Actor, PoseSymbol Pose) : Instruction;

/// <summary>Offers the options of a choice. The choice is skipped when none is available.</summary>
/// <param name="Options">The options, in source order.</param>
/// <param name="Join">Where flow continues after an option's body.</param>
internal sealed record ChoiceInstruction(IReadOnlyList<CompiledOption> Options, int Join) : Instruction;

/// <summary>One option of a choice.</summary>
/// <param name="Id">The option's ID, which the host picks it by and which records it as chosen.</param>
/// <param name="Text">The option's text, which is also in the story's templates under <paramref name="Id"/>.</param>
/// <param name="Condition">The <c>@if</c> condition, or <see langword="null"/>.</param>
/// <param name="IsOnce">Whether picking the option removes it for good.</param>
/// <param name="Body">The index where the option's body starts, which is the choice's join if the body is empty.</param>
internal sealed record CompiledOption(string Id, Template Text, Expr? Condition, bool IsOnce, int Body)
{
    /// <summary>The option's tags.</summary>
    public IReadOnlyList<TemplateTag> Tags => Text.Tags;
}

/// <summary>Continues at <paramref name="Target"/> in this node.</summary>
internal sealed record BranchInstruction(int Target) : Instruction;

/// <summary>Continues at <paramref name="Target"/> in this node when the condition is false, and at the next instruction when it's true.</summary>
internal sealed record BranchIfFalseInstruction(Expr Condition, int Target) : Instruction;

/// <summary>Sets a variable. <c>+=</c> and <c>-=</c> are lowered to a plain set of a sum or difference.</summary>
internal sealed record SetInstruction(VariableSymbol Variable, Expr Value) : Instruction;

/// <summary>Pauses the story for a duration.</summary>
/// <param name="Duration">How long to wait.</param>
/// <param name="Location">Where the <c>@wait</c> is written, for a warning.</param>
internal sealed record WaitInstruction(Expr Duration, SourceLocation Location) : Instruction;

/// <summary>Runs a command.</summary>
/// <param name="Command">The command.</param>
/// <param name="Arguments">The arguments, in the order of the command's parameters, with defaults filled in.</param>
/// <param name="Waits">Whether the story waits for the command to finish: its declared <c>waits</c>, overridden by <c>wait</c> or <c>nowait</c>.</param>
/// <param name="Location">Where the command is written, for a warning about its arguments.</param>
internal sealed record CommandInstruction(CommandSymbol Command, IReadOnlyList<Expr> Arguments, bool Waits, SourceLocation Location) : Instruction;

/// <summary>Continues at the start of another node, leaving the call stack as it is (<c>@jump</c>).</summary>
/// <param name="Node">The node's current name.</param>
internal sealed record JumpInstruction(string Node) : Instruction;

/// <summary>Runs another node, then comes back (<c>@call</c>). It has an ID because a save can be waiting inside the called node.</summary>
internal sealed record CallInstruction(string Node, string Id) : Instruction;

/// <summary>Returns from the current <c>@call</c>, or ends the dialogue at the top level. Every node ends with one.</summary>
internal sealed record ReturnInstruction : Instruction;

/// <summary>Ends the dialogue and clears the call stack.</summary>
internal sealed record EndInstruction : Instruction;

/// <summary>Which alternative of a block runs, from the number of times it has been entered.</summary>
internal enum BlockKind
{
    /// <summary><c>@sequence</c>: alternative <c>min(n, count - 1)</c>.</summary>
    Sequence,

    /// <summary><c>@cycle</c>: alternative <c>n mod count</c>.</summary>
    Cycle,

    /// <summary><c>@once</c>: its one alternative when <c>n</c> is 0, otherwise it goes straight to the exit.</summary>
    Once,
}

/// <summary>Enters a block that varies with how often it's entered. The runner reads <c>n</c> for the block's ID, stores <c>n + 1</c>, and continues at the pick.</summary>
/// <param name="Kind">How the alternative is picked.</param>
/// <param name="BlockId">The block's ID, which its entry count is kept under.</param>
/// <param name="Alternatives">The index where each alternative starts.</param>
/// <param name="Exit">Where flow continues after an alternative, and where a skipped <c>@once</c> block goes.</param>
internal sealed record VariationInstruction(BlockKind Kind, string BlockId, IReadOnlyList<int> Alternatives, int Exit) : Instruction;
