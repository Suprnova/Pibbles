# Runtime design

The runtime compiles a bound story to instructions and runs it: the compiler, the dialogue runner, the story state and its snapshots, line rendering and the reveal helper.

## Compilation

`StoryCompiler.Compile(sources, options)` (in `Pibbles.Compiler`) takes the sources and the compilation options, and returns a `CompileResult`: the `Story`, every diagnostic after the files' `.editorconfig` severities, and `HasErrors`. `Story` is `null` exactly when `HasErrors` is true, that is, when any diagnostic is an error, including a warning raised to one. A story with only warnings and hints compiles, and its diagnostics are still in the result.

The compiler lowers each node's syntax tree to a **flat list of instructions**, reading what the binder worked out from the [bindings](semantics.md#bindings): the symbol each name refers to, and each expression's type, including a number that its context uses as a duration. The compiler never resolves a name itself. The instructions are:

| Instruction | Lowered from |
| --- | --- |
| `Line(id)` | A text line that shows text, with or without a speaker. `mira: {w}` is one too. The line's template is stored under `id`. |
| `Pose(actor, pose)` | A pose-only line (`mira (sad):`), and, before the `Line`, every posed line (`mira (happy): Hi`), every time, even if the actor already has that pose. A host treats a repeat as a no-op, and a host redrawing after a load can trust it. |
| `Choice(options, join)` | Consecutive `->` options. Each option has its ID, its text template, its condition (from `@if`, or none), whether it is `@once`, its tags (in the template), and the index where its body starts. Options keep source order. |
| `BranchIfFalse(condition, target)`, `Branch(target)` | `@if` / `@elif` / `@else`, and the branches that join them. A target is an index in the same node. |
| `Set(variable, value)` | `@set`. `+=` and `-=` are lowered to a plain `Set` of a sum or difference, so `$x -= 1` is `Set($x, $x - 1)`. |
| `Wait(duration)` | `@wait`. |
| `Command(command, arguments, waits)` | A command line. Arguments are in parameter order, every parameter filled, an omitted one from its default. `waits` is the command's declared `waits`, overridden by `wait` or `nowait`. |
| `Jump(node)` | `@jump`. Continues at the node, leaving the call stack as it is. The target is the node's current name, even when the story wrote an old one. |
| `Call(node, id)` | `@call`. It has an ID because a save can be waiting inside the called node. |
| `Return`, `End` | `@return` (at the top level it acts as `@end`) and `@end`, which ends the dialogue and clears the call stack. Every node's list ends with an implicit `Return`. |
| `Variation(kind, blockId, alternatives, exit)` | `@sequence`, `@cycle` and `@once`. `alternatives` are the indexes where each alternative starts. |

`Branch` and `BranchIfFalse` move within a node, and `Jump` and `Call` go to another one, so a local index is never mistaken for a node.

**Layout.** A body that doesn't always leave ends with a `Branch` to the place flow continues: the choice's `join`, a variation's `exit`, or the end of an `@if`. Every body gets one, including the last, so the layout is uniform. An option with an empty body starts at `join` and has no instructions. A body *always leaves* when one of its statements is `@jump`, `@end` or `@return`, or an `@if` with an `@else` whose branches all leave, the rule behind PIB3001 ([flow checks](semantics.md#passes)). Conditions are never evaluated, and a nested choice or variation never counts. An `@if` clause is `BranchIfFalse` to the next clause, its body, then `Branch` to the end; the `@else` body just falls through.

The runner's side of each layout:

- **Choices.** Options are sticky unless `@once`, which removes the option for good once it's picked. An option with a false `@if` is unavailable, but the host still sees it, flagged. If no option is available, the choice is skipped. Picking an option records it as chosen, keyed by its ID, before its body runs. The host picks by the option or its ID, never an index.
- **Variations.** Each block has an entry count `n`, keyed by its ID. On entry the runner reads `n`, stores `n + 1`, then runs the pick: `@sequence` runs alternative `min(n, count - 1)`, `@cycle` runs `n mod count`, and `@once` runs its one alternative only when `n` is 0, otherwise it goes straight to `exit`. The count rises even when the pick ends in `@jump` or `@end`.

Why a flat instruction list instead of walking the tree:

- **The runner pauses after every step.** A tree walker would have to keep an explicit stack of cursors, which amounts to a program counter anyway. A flat list makes it `(node, index)`.
- **Serializable position.** `(node, index)` plus a call stack of the same shape is the whole execution state.
- **Easy to test and dump.** An IR listing is a readable snapshot.

**Expressions** stay as small trees, evaluated recursively. They never pause, so a stack machine would gain nothing. Lowering builds them from the syntax and the bindings, so evaluating one needs neither: every name is already its symbol, and every node knows its type. The nodes are constants (number, duration in seconds, string, bool, and an enum member, actor or node as its symbol, a node by its current name), variables, calls to host functions (arguments in parameter order, defaults filled), `visits(node)` as a node of its own, unary and binary operators, and an explicit conversion. Every node carries its `Type`, so an operator's operand types (`Left.Type`, `Right.Type`) say whether `+` adds numbers, adds durations or joins text. Where the bindings say the context uses a number as a duration, lowering wraps that expression in a `ToDuration`: for `0.5s + (1 + 2)` the wrapper goes around the parenthesized sum. Nothing is folded or evaluated at this stage; division by zero, `%` and overflow are the evaluator's.

**Line templates** are the lowered inline text of each line and option, stored by ID. A template has the speaker (an actor or none), every tag with its name, raw value and, for an enum-typed tag, the member, and its content: a list of elements, each of which is text, a markup span (with its arguments in parameter order and its children), a shown value (a `string`, `number` or `actor` expression), an inline command (with its resolved `waits`), `{w}` (input wait), `{w d}` (timed pause), `{speed x}`, `{speed}` (reset), `{p}`, `{br}`, `{icon name}`, or conditional text with its `{if}`/`{elif}` branches and `{else}`. Speed is a point: `{speed x}` sets a factor relative to the player's setting from there on, and `{speed}` returns to it. Whether a computed pacing value is greater than zero is checked when the line is rendered, not here.

**IDs.** Lines that show text, options, `@call` and the openers of `@sequence`, `@cycle` and `@once` have an ID, from their `#id` tag. Pose-only lines have none. A site with no `#id` gets a **fallback ID**, `~<path>:<line>`, where `path` is the source's path as given and `line` is the 1-based line of the site. No written ID can start with `~`, and the same sources always give the same fallback IDs. The story records which IDs are fallbacks, because a snapshot can't keep state keyed by one ([localization](localization.md#when-theyre-required)).

**The `Story`** holds everything the runner needs and nothing from the sources: it doesn't keep the compilation. It has the nodes by name; each `#was:` name mapped to the node's current name, so a host can start a node by either; the variables with their starting values as expressions; the templates by ID; and, for restoring saves, a table from each ID to where it lives, `(node, index)`. An option's site is its choice. All of it is internal, and the `Story` itself has no public members yet.

**When lowering fails.** Lowering only runs on sources with no errors, so every name resolved. A missing binding is a bug, not bad input, and throws `InvalidOperationException` naming the node and the source location.

## Runtime model

```csharp
var result = StoryCompiler.Compile(sources);
if (result.HasErrors) { /* report result.Diagnostics */ }

var functions = new HostFunctions()
    .Add("has_item", (string id) => inventory.Contains(id));

var state = new StoryState(result.Story, seed);        // or StoryState.Restore(story, snapshot)
var runner = new DialogueRunner(result.Story, state, functions);

runner.Start("kitchen.door");
while (true)
{
    switch (runner.Next())
    {
        case LineStep { Line: var line }: /* show, wait for player */ break;
        case ChoiceStep { Options: var options }: runner.Choose(/* index */); break;
        case CommandStep { Command: var command, Wait: var wait }: /* run; await if wait */ break;
        case PoseStep { Actor: var actor, Pose: var pose }: /* swap sprite */ break;
        case WaitStep { Duration: var duration }: /* delay */ break;
        case EndStep: return;
        default: break;                                 // step kinds added by extensions
    }
}
```

- **Pull-based, synchronous, single-threaded.** The core has no `async`, no events and no timers. It has no skip mode either: skipping is the caller asking for steps without waiting ([skipping](godot.md#skipping)). Hosts wrap it in whatever model their engine prefers. The Godot adapter uses signals and `await`. This keeps the core trivially testable.
- **Steps are immutable records.** Pattern matching on them is the whole host API for flow. Extensions add step kinds, so a host's `switch` keeps a default arm.
- **Host functions** are registered as typed delegates through generic `Add` overloads (AOT-safe, no reflection). `HostFunctions.Validate(story)` reports missing or mistyped functions, and adapters call it at startup.
- **Command arguments** arrive as a `CommandInvocation` with typed accessors by parameter name (`command.GetActor("who")`, `command.GetNumber("strength")`). Enum values come through as their member names.
- **The seed is chosen by the host** when it creates a new state, usually at random for a new game. v1 stores it without using it, so adding randomness later doesn't change the API.
- **Several runners can share one `StoryState`,** for example ambient remarks during room play while the main dialogue is suspended. Everything is single-threaded.
- **Node names from the host resolve through aliases.** `Start`, `node` values restored from a snapshot, and anything else the host passes in accept a node's current name or any of its `#was:` aliases. A scene that still says `kitchen.front_door` keeps working after the node is renamed.
- **Misuse throws; content never does.** Calling `Next()` while a choice is waiting, choosing an out-of-range or unavailable option, starting a node that doesn't exist, or reaching a host function that was never registered throws `InvalidOperationException`. For an unknown node, the message suggests the closest name. A story that passed analysis can't cause a runtime type error. Host functions are checked at registration.

## Lines and text

```csharp
public sealed record Line(
    string Id,
    string? Speaker,                 // actor ID; null for narration
    string? SpeakerName,             // the actor's display name
    string Text,                     // plain text, fully resolved; icons are U+FFFC
    ImmutableArray<Span> Spans,      // (Markup, Arguments, Start, Length)
    ImmutableArray<Marker> Markers,  // (Position, Kind, payload): pause, input wait, page, speed (the factor after it), command
    ImmutableArray<Tag> Tags);
```

- Rendering a template resolves interpolation and conditionals against the state, producing plain text plus spans and markers. Spans and markers are positioned in the resolved text.
- **Positions are UTF-16 indices** into `Text`, in logical order (which is what .NET strings use). Adapters convert them if their engine counts differently.
- `{br}` becomes a `\n` in `Text`. `{p}` and `{w}` are markers only.

### The reveal helper

```csharp
var reveal = new LineReveal(line, new RevealSettings(CharactersPerSecond: 40, Instant: false));

RevealFrame frame = reveal.Advance(delta);   // frame.VisibleLength, frame.Fired (markers crossed), frame.State
reveal.Skip();                               // jump to the next stop point; fires skipped effect markers
reveal.Resume();                             // continue after an input wait, page break or blocking command
```

- **States:** `Revealing`, `WaitingForInput` (after `{w}` or `{p}`), `WaitingForHost` (after a blocking inline command, until `Resume()`), `Complete`.
- **Reveals move by grapheme cluster** (`StringInfo`), so emoji and combining marks never show half-drawn. Segmentation follows the runtime's Unicode version, so .NET 8 and .NET 10 can split the newest emoji differently. Reveal fixtures shared by both targets avoid them.
- **Timing:** `{speed x}` multiplies the base rate from that point on, and `{speed}` returns to it. Speed resets at the start of every line, and each speed marker carries the factor in effect after it. A speed factor, pause or wait computed at run time that isn't greater than zero is skipped with a warning; constants are rejected at check time ([PIB2039](diagnostics.md#pib2039)). `Instant` shows everything at once but still stops at input waits. Optional extra pauses after punctuation are a setting, not part of the language.
- **Skip semantics** are defined once, here: effect markers fire in order, timing markers are dropped ([reference](language/reference.md#runtime-semantics-summary)).

## State and saves

`StoryState` holds everything that belongs in a save slot:

- `@var` values
- Node visit counts
- Variation block entry counts (by block ID)
- Options already chosen (keyed by option line ID), for `WasChosen` and `@once`
- Current pose of each actor
- The seed

State that outlives a save slot, such as unlocks or read tracking, belongs to the game ([boundaries](boundaries.md)).

- `state.CreateSnapshot()` and `StoryState.Restore(story, snapshot)` use plain records. The core provides JSON helpers built on `System.Text.Json` source generation, which is AOT-safe. The host decides where snapshots are stored.
- **Snapshots carry a format version,** separate from the story's version. The language grows by [extensions](language/design.md#grammar-extensions) that add state ([below](#with-extensions)), so a newer core must restore every older format, filling new state with its defaults.
- **Keys are chosen to survive script edits.** Variables are keyed by name, visits by node name (following `#was:` aliases, so a renamed node keeps its visit count), line and option state by line ID, and variation blocks by their own `#id`. So inserting, deleting or reordering blocks, moving one to another node, or renaming its node never hands one block's state to another.
- **Variation blocks store an entry count, not a position.** Each block records how many times execution has entered it, and its kind derives what to run from that count ([reference](language/reference.md#variations)). A saved count therefore has a defined meaning whatever happens to the block afterwards: alternatives added or removed, or its kind changed.
- **When restoring a snapshot, state entries that no longer match the story** (removed variables, changed types, counts for removed lines and blocks) are dropped. Each one is reported in a `RestoreReport` instead of throwing, so an old save still loads after a patch. A dialogue's *position* needs more care, covered [below](#restoring-after-an-update).

### Saving mid-dialogue

Hosts save during dialogue, so the position of a running dialogue is part of a save too. The design rests on one rule: **every position a save can point to has an ID,** so restoring is an exact lookup, never a guess.

#### Save points

- **A dialogue's position is always a line or a choice.** Those are the moments it waits on the player.
- **The player can still save at any time.** If a save is requested during any other step (a blocking command, a `@wait`, a pose change), the adapter **fast-forwards** first. It finishes the current step instantly, [skips](godot.md#skipping) to the next line or choice, and takes the snapshot there. This is the same mechanism as skip mode, so it adds nothing new for the host. If the dialogue ends before reaching another line, the save simply has no dialogue in progress.
- **Only real line IDs are saved.** A line without an `#id` yet has a fallback identity that exists only in memory ([localization design](localization.md#when-theyre-required)). A snapshot leaves out state keyed by one, and a dialogue waiting on such a line saves as no dialogue in progress. The snapshot's report lists both. This only happens in development, since `pibbles check --warnaserror` rejects lines without IDs.
- **Loading shows the saved line or choice again.** With inline variations, the line is rendered without bumping its show count, so it picks the same alternatives as before. Text that depends on host functions can still differ, since the game's own state may have changed. The reveal starts over and its inline markers fire again. That's why only commands declared `inline` may appear in text ([reference](language/reference.md#commands)). A restored choice evaluates its options' conditions again against the current state. If none is available, it's skipped like any other choice, and the `RestoreReport` says so.
- **The runner snapshot is separate from the state snapshot,** because several runners can share one state. It holds the story's version (from `pibbles.json`) and the call stack as a list of IDs. The top frame is the ID of the line, or, for a choice, the IDs of all its options in source order, including any that were hidden, unavailable or already used up. A choice has no ID of its own, so the whole set identifies it, and any one survivor is enough to find it again. Every frame below it is the ID of the `@call` it's waiting on, since calls carry IDs too ([localization design](localization.md#what-needs-an-id)). The host stores both snapshots in its save file.
- **Loading:** `DialogueRunner.Restore(story, state, functions, snapshot)`, followed by `Next()` as usual.

#### What the host saves

- **The host restores what's on screen.** Pibbles restores what it owns: variables and poses. Which actors are staged where, the background and the music are host state, and the host saves them alongside. The host already has this state in its stage manager, so serializing it there is simpler than having Pibbles rebuild the stage.
- **The host saves its intended state, not animation in progress.** A stage manager that records each command's target when the command is issued is already consistent after a fast-forward.
- **Long gameplay handoffs aren't blocking commands.** Fast-forwarding would skip a command that waits for a whole minigame. Handoffs like that end the dialogue and give a continuation node instead (`@enter_room kitchen on_exit=kitchen.leave`), and the game saves its own gameplay state.

#### Restoring after an update

A saved ID still exists after an update as long as the line keeps its `#id`, however it was reworded or wherever it moved. The dialogue resumes there. For a choice, any of its saved option IDs that still exists finds the choice, and the whole choice is shown again, so deleting or reordering options never loses a save.

If no saved ID exists any more, that frame is lost. Control unwinds to the frame below, or the dialogue ends, and the `RestoreReport` says so. In v1 this happens whenever an update deletes a line, or all of a choice's options, that a save is waiting on. Keep a replaced line's ID on its replacement rather than deleting the line. [Aliases and migrations](#release-manifests-and-migrations) give retired IDs somewhere to go later.

**Nothing is guessed.** If the right place to resume isn't obvious, a person decides.

## Performance

Compiling a large VN from source is expected to take tens of milliseconds, and a runtime step to take microseconds. These are estimates to confirm with the benchmark project in Phase 3. The reasoning:

- A big visual novel runs to hundreds of thousands of words, a few megabytes of script. A hand-written, line-oriented parser over `ReadOnlySpan<char>` processes text in the tens of megabytes per second. Binding is a few hash lookups per reference.
- A runtime step is one instruction plus rendering a template of a few dozen elements. That's well below one frame even on weak hardware.

Decisions that follow:

- **v1 compiles from source when the game loads.** A precompiled binary format is a stretch goal, justified only if measurements show load time matters or the game wants to ship without the compiler.
- **Readability comes first.** The benchmark project (`Pibbles.Benchmarks`, BenchmarkDotNet) exists to catch regressions and find real hotspots, not to justify optimizing ahead of time.
- **Language-server latency** is driven by re-binding the whole project on each edit. Incremental binding is deferred until a real story makes it measurably slow.

## With extensions

What the [grammar extensions](language/design.md#grammar-extensions) and later tiers ([roadmap](roadmap.md#feature-tiers)) add to the runtime. Each part moves into the sections above when its feature ships.

### Drafts

`StoryCompiler.Compile` takes a build profile. **Development** includes [drafts](language/design.md#drafts). **Release** leaves them out entirely before binding, so a release compile reports exactly what would break without them. The Godot adapter picks the profile by build type ([Godot design](godot.md#drafts)). `pibbles check --release` compiles with the release profile.

### Personas

- `PersonaStep { Actor, Persona }` is emitted whenever an actor's active persona changes, so the host can swap sprites already on screen, for example to or from a silhouette.
- `Line` gains `Persona` (the active persona, null for the base presentation), and `SpeakerName` comes from the persona when it overrides the name.
- Personas aren't stored. They're worked out from variables, so they restore automatically.

### Mid-line poses and `{auto}`

`Line.Markers` gain `pose` and `auto` kinds. The flow checks report `{auto}` anywhere but the end of a line.

### Randomness

- `StoryState` holds the random generator's seed and position. The generator is a small algorithm in the core, not `System.Random`, whose state can't be serialized and whose seeded sequence isn't guaranteed across .NET versions. `@shuffle` adds whatever per-round state it needs ([language design Q21](language/design.md#open-questions)).
- Runners that share a state share the generator, so the order in which the host steps them is part of the input.
- `pibbles play --seed` and tests pass a fixed seed. The determinism property includes the seed.

### Inline variations

`StoryState` holds line show counts, by line ID. Loading a save re-renders the saved line without bumping its count, so it picks the same alternatives as before. Saves from before this extension count every line as unseen ([known exception](language/reference.md#extension-compatibility)). Line templates gain variations.

### Localization

- `DialogueRunner.Locale` selects the language, and rendering resolves templates against the state and the locale. A translation compiles to a template of the same shape as the source ([localization design](localization.md)).
- `Line.SpeakerName` is the localized display name.

### Release manifests and migrations

`pibbles release <version>` records what a shipped build contained in a committed file, `story/releases/<version>.json`. It lists every ID a save could point to (drafts excluded), with each choice's option IDs grouped, every node and variable name, and for each node the order of its IDs and state-changing statements (`@set`, `@call`, `@jump`, commands not declared `inline`). From then on, `pibbles check` compares the story against every recorded release:

| Check | Severity |
| --- | --- |
| A shipped ID resolves through none of the steps below. A shipped choice resolves if any of its option IDs does. | Error |
| A shipped node name no longer exists, and has neither a `#was:` alias nor a migration claiming the node. Visit counts and host-held continuations like `on_exit=` refer to nodes by name. | Error |
| A shipped ID moved into a node that its shipped node reaches neither by a single `@call` nor by `@jump`. Restoring still lands on it, but continues through its new node's flow. | Warning, resolved by a migration if the new flow is wrong for old saves |
| A shipped variable no longer exists and no `@var` lists it with `#was:`. Old saves lose its value. | Warning |
| A state-changing statement was added or removed before a shipped ID in the same node. Saves from that release resume after the change and never see it: the "missed `@set`" softlock. | Warning, resolved by a migration scoped to that release (`#migrates:h8ya3k@1.0`), which runs only for saves made with that version |

CI runs `pibbles check --release --warnaserror`, so an update can't ship until every save from every earlier release has somewhere correct to go.

**Nobody types lists of retired IDs by hand.** The language server offers a quick fix on these errors that writes the migration header. It uses the coarsest claims that fit: a whole node when all of that node's retired IDs are unclaimed, and contiguous ranges otherwise. The writer only fills in the body.

Release manifests also change how saves resolve. Each saved ID goes through the first of these that applies:

1. **The ID still exists:** resume there, as in v1. If it moved into a node that its shipped node now `@call`s from exactly one place (what **Extract to node** produces, [semantics design](semantics.md#extract-to-node)), restoring adds that missing call frame, so the dialogue returns where it used to continue.
2. **A line lists it as a former ID** (`#was:k7qp2x`): resume at that line. Use this when a line is replaced, or deleted and a nearby line is the right place to pick up.
3. **A migration node claims it,** by ID, by a range of shipped IDs, or by the whole shipped node it belonged to (`#migrates:k7qp2x`, `#migrates:k7qp2x..m3xw9a`, `#migrates:cellar.old_subplot`). Run that node, which fixes up state and ends with `@resume <id>` at the correct post-update point. The most specific claim wins ([language design](language/design.md#migrations)).
4. **Nothing claims it:** the frame is lost, as in v1. A released build guarantees this never happens.

A choice frame tries each step on all of its option IDs, in saved order, before moving on to the next step. So a surviving option always wins over an alias or a migration for one of its removed siblings. Variables also follow `#was:` aliases on `@var`, and line state follows line `#was:` aliases.

The **save across releases** property tests this: record a release, make random edits until `pibbles check` passes against it, then restore saves from every shipped position. Each one lands exactly where its ID, alias or migration says, and never throws.
