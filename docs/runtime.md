# Runtime design

The runtime compiles a bound story to instructions and runs it: the compiler, the dialogue runner, the story state and its snapshots, line rendering and the reveal helper.

## Compilation

`StoryCompiler.Compile(sources, options)` (in `Pibbles.Compiler`) takes the sources and the compilation options, and throws `ArgumentException` if two sources have the same path, which is a mistake in how the host gathered them. Otherwise it never throws, and returns a `CompileResult`: the `Story`, every diagnostic after the files' `.editorconfig` severities, and `HasErrors`. `Story` is `null` exactly when `HasErrors` is true, that is, when any diagnostic is an error, including a warning raised to one. A story with only warnings and hints compiles, and its diagnostics are still in the result.

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

var state = new StoryState(result.Story!, seed);       // or StoryState.Restore(story, snapshot)
var runner = new DialogueRunner(result.Story!, state, functions);

runner.Start("kitchen.door");
while (true)
{
    switch (runner.Next())
    {
        case LineStep { Line: var line }: /* show, wait for player */ break;
        case ChoiceStep { Options: var options }: runner.Choose(/* an option, or its ID */); break;
        case CommandStep { Command: var command, Waits: var waits }: /* run; await if waits */ break;
        case PoseStep { Actor: var actor, Pose: var pose }: /* swap sprite */ break;
        case WaitStep { Duration: var duration }: /* delay */ break;
        case EndStep: return;
        default: break;                                 // step kinds added by extensions
    }
}
```

- **Pull-based, synchronous, single-threaded.** The core has no `async`, no events and no timers. It has no skip mode either: skipping is the caller asking for steps without waiting ([skipping](godot.md#skipping)). The one exception is `FastForward()`, which skips to the next save point ([below](#save-points)). Hosts wrap it in whatever model their engine prefers. The Godot adapter uses signals and `await`. This keeps the core trivially testable.
- **Steps are immutable records,** all deriving from `DialogueStep`. Pattern matching on them is the whole host API for flow. Later versions add step kinds, so a host's `switch` keeps a default arm.
- **Host functions** are registered as typed delegates through generic `Add` overloads (AOT-safe, no reflection), described [below](#host-functions). `HostFunctions.Validate(story)` reports missing or mistyped functions, and adapters call it at startup.
- **Command arguments** arrive as a `CommandInvocation` with typed accessors by parameter name (`command.GetActor("who")`, `command.GetNumber("strength")`). Every parameter has a value, since defaults are filled in. The accessors are `GetBool`, `GetString`, `GetNumber` (`decimal`), `GetDuration` (`TimeSpan`, clamped like host function durations, with an `Overflow` warning), `GetActor` (the ID), `GetNode` (the current name), `GetEnum` (the member name) and `GetEnum<TEnum>`, which parses the name into the host's own enum without reflection. An unknown parameter name throws `ArgumentException`, and the wrong accessor for a parameter's type throws `InvalidOperationException`.
- **The seed is chosen by the host** when it creates a new state, usually at random for a new game. v1 stores it without using it, so adding randomness later doesn't change the API.
- **Several runners can share one `StoryState`,** for example ambient remarks during room play while the main dialogue is suspended. Everything is single-threaded.
- **Node names from the host resolve through aliases.** `Start`, `node` values restored from a snapshot, and anything else the host passes in accept a node's current name or any of its `#was:` aliases. A scene that still says `kitchen.front_door` keeps working after the node is renamed.
- **Misuse throws; content never does.** Calling `Next()` before `Start` or while a choice is waiting, calling `Choose` when none is waiting or with an option that isn't offered or isn't available, starting a node that doesn't exist, taking a runner snapshot away from a save point, or reaching a host function that was never registered throws `InvalidOperationException`. After an `EndStep`, `Next()` returns `EndStep` again until `Start` is called. For an unknown node, the message suggests the closest name. A story that passed analysis can't cause a runtime type error. Host functions are checked at registration.

### Steps

| Step | The host | The runner did first |
| --- | --- | --- |
| `LineStep(Line)` | Shows the line and waits for the player. | Rendered the line's text. |
| `ChoiceStep(Options)` | Shows the options, and answers with `Choose(option)` or `Choose(id)`, never an index. | Evaluated the options' conditions and texts. |
| `CommandStep(Command, Waits)` | Carries the command out, and waits for it to finish if `Waits`. | Evaluated the arguments, in parameter order with defaults. |
| `PoseStep(Actor, Pose)` | Changes the actor's pose. It's sent every time, even for the pose the actor already has, so a host that redraws can trust it. | Set the pose in the state. |
| `WaitStep(Duration)` | Pauses. | Evaluated the duration. One that isn't more than zero is skipped with a `NonPositiveWait` warning. |
| `EndStep` | Ends the dialogue. | Cleared the call stack. |

Everything else (`@set`, branches, `@jump`, `@call`, `@return`, variations) the runner does between steps.

**Choices.** The runner delivers every option except an `@once` option that has already been chosen, in source order, with its conditions evaluated when the choice is reached. A `ChoiceOption` has the option's text as a `Line` (its `Id` is the option's ID, and it has no speaker), `IsAvailable` (its `@if`, or true) and `WasChosen`. The host decides whether an unavailable option is hidden or greyed out. If none is available the choice is skipped. `Choose` records the option as chosen before its body runs, and `Next()` then continues in the body; when the body finishes without leaving, flow continues after the whole choice.

**Lines** are rendered as [described below](#lines-and-text).

**Visits.** A node's visit count goes up on `Start`, `@jump` and `@call` into it, before its first statement, so a node that jumps to itself counts its rounds. It doesn't go up on `@return`, or when a save is restored.

**`StoryState`** holds what a save slot holds: variable values (starting from their declared starting values), visit counts by node name, variation block entry counts by block ID, the IDs of chosen options, each actor's current pose (`GetPose(actor)`, which is the actor's first pose until the story sets one), and the seed. A variable's starting value can read variables declared before it, but not call a function.

**Atomicity.** An instruction evaluates everything it needs before it changes anything. When one throws, such as a `HostFunctionException` from a condition, a command argument or a line's text, the state and the runner's position are unchanged, so the host can recover and call `Next()` again to retry it. Instructions that finished earlier in the same call stay finished, and warnings the failed instruction raised are dropped.

**Warnings reach the host** through `RunnerOptions.OnWarning`, a callback the runner calls after the instruction that raised the warning has finished. Without one, warnings are dropped. Nothing about the story's flow depends on it. (Warnings from a command's duration are reported when the host reads it, since that is when the clamp happens.)

**The loop budget.** `Next()` runs at most `RunnerOptions.InstructionBudget` instructions (100,000 by default) without producing a step. Past that it reports an `InfiniteLoop` warning and ends the dialogue with an `EndStep`, so a story can never hang the game.

### Values

The runtime's values are the language's types: `bool`; `number`, an exact `decimal`, so `0.1 + 0.2` is exactly `0.3`; `string`; `duration`, `decimal` seconds (`TimeSpan` appears only at the host function edge); an enum member, an actor, and a `node`, held by its current name. Equality is by value within one type: numbers and durations compare numerically (`1.50 == 1.5`), strings by exact characters, and members, actors and nodes by identity. The binder has already rejected comparisons across types. `Value` is internal; the public API has typed accessors instead.

Expressions are evaluated recursively against an evaluation context that answers a variable's value, a node's visit count (when a visit counts is the runner's business) and host function calls, and receives runtime warnings. The semantics of the operators, including division by zero, overflow, floored `%` and short-circuiting, are in the [reference](language/reference.md#operator-types). Evaluating never throws for content and changes nothing but through host functions, so a throw from one can't leave a half-done step.

### Host functions

```csharp
var functions = new HostFunctions()
    .Add("has_item", (string id) => inventory.Contains(id));
```

`Add` has overloads for zero to four parameters, and `AddDynamic(name, parameterTypes, returnType, function)` registers a function whose types are only known when the program runs, such as one a scripting bridge forwards to another language: the delegate takes its arguments and returns its result as host values (`object?[]` in, `object?` out), and the registration is validated the same way. Types are strict, and there is one mapping for every value a host sees, wherever it meets one:

| Story type | Host type |
| --- | --- |
| `bool` | `bool` |
| `number` | `decimal` |
| `string` | `string` |
| `duration` | `TimeSpan`: decimal seconds rounded to the nearest tick (100 ns). A duration past `TimeSpan`'s range clamps, with an `Overflow` [warning](#runtime-warnings). A `TimeSpan` going the other way becomes exact seconds. |
| An enum member | `string`: the member's name |
| `actor` | `string`: the actor's ID |
| `node` | `string`: the node's current name (an old `#was:` name is accepted on the way in) |

That is the mapping for function arguments and results, for a command's and a span's arguments (`GetValue`), and for variables (`StoryState.GetVariable` and `SetVariable`). `StoryType.HostType` gives the .NET type for a story type. A string for an enum member, an actor or a node is checked against the story wherever a host passes it in. Nothing else is accepted: no `int`, `float`, `double` or C# enum, so a game converts its own floats. A function declared to return an enum, actor or node returns a string that must name a member, a declared actor, or a node (its current name or a `#was:` name); anything else is a host error. Functions must be free of side effects: `and` and `or` short-circuit, and text is rendered again after a load.

- **`Add` throws `ArgumentException`** for a name registered twice, a null delegate, an unsupported type, or `visits`, which the core answers itself.
- **`Validate(story)`** returns a list of `HostFunctionProblem`, never throwing: a declared function that's missing, a registered one whose parameters or return type don't match ("In the story, `has_item` takes a `string` and returns a `bool`, but the registered function takes a `decimal` and returns a `bool`"), and a registered name the story doesn't declare, which is likely a typo. They describe the host's code, so they aren't diagnostics. Hosts keep a default arm on `HostFunctionProblemKind`, which can grow.
- **At run time,** calling a function that was never registered throws `InvalidOperationException`, which `Validate` exists to catch first. A function that throws, or returns a string that names nothing, is wrapped in a `HostFunctionException` that names the function and the story location, with the original as `InnerException`. The Godot adapter catches it and falls back ([failures](godot.md#failures)).

### Story metadata and variables

A compiled story describes itself to hosts, read-only: `story.Functions` (each with its parameters' names and types and its return type), `story.Variables` (name and type), `story.Actors` (ID, display name and poses, the first being the default), `story.Enums` (name and members) and `story.Nodes` (current name and `#was:` names). A type is a `StoryType`: a kind (`Bool`, `Number`, `Text`, `Duration`, `Enum`, `Actor` or `Node`), plus the enum's name for an enum.

`StoryState.GetVariable(name)` reads a variable as a host value (a duration variable beyond `TimeSpan`'s range clamps silently without a warning, because the state has nowhere to send one), and `SetVariable(name, value)` sets one, checked against its type: a wrong .NET type, a string that names no member, actor or node, or an unknown variable throws `ArgumentException`. Hosts use them for debug overlays and seeding. `CommandInvocation` and a span's `Arguments` list their `Parameters` (names and types) and give any value with `GetValue(name)`, so a host can forward a command it doesn't know at compile time, for example as a dictionary to GDScript. A `Line` says whether its ID is a fallback (`IsFallbackId`), which a host that records the lines the player has seen must skip, because a fallback ID changes whenever the file is edited. A `ChoiceOption` has a `Number`, its place among the choice's options in the source, counting options `@once` has removed, so it is the same on every visit; it is for display and tools, and `Choose` still takes the option or its ID.

### Runtime warnings

Three kinds of problem, three channels:

- **Diagnostics are static.** They report what analysis finds before the story runs.
- **Exceptions are for the host.** A host that misuses the API, or a host function that fails, throws.
- **Runtime warnings are for content the story carries on from.** A `RuntimeWarning` has a `Kind` (a public enum, so hosts filter by it and keep a default arm, since kinds are added), a message written for writers, and the story location (path and line). It has no `PIB` code and isn't in the diagnostic catalog. Content problems never throw, so the story always carries on.

| Kind | When it happens | What the story does instead | How to fix the script |
| --- | --- | --- | --- |
| `DivisionByZero` | `/` or `%` with a divisor that is zero at run time | The result is `0` | Check the divisor first, or divide by something that can't be zero |
| `NonPositivePause` | A `{w d}` in a line whose duration, computed at run time, isn't more than zero | The pause is left out of the line | Make sure the duration can't be zero or negative |
| `NonPositiveSpeed` | A `{speed x}` in a line whose factor, computed at run time, isn't more than zero | The speed change is left out of the line | Make sure the factor can't be zero or negative |
| `NonPositiveWait` | An `@wait` whose duration, computed at run time, isn't more than zero | The wait is skipped and the story carries on | Make sure the duration can't be zero or negative |
| `InfiniteLoop` | A dialogue runs through the instruction budget without showing anything | The runner ends the dialogue | Give the loop a way out, such as a condition on a variable it changes |
| `Overflow` | A result past about ±7.9 × 10^28 from `+`, `-`, `*` or `/`; or a duration too long for a `TimeSpan` passed to a host function | The largest or smallest value it can hold, with the true result's sign | Use smaller numbers |

The runner [reports warnings](#steps) to the host through its options.

## Lines and text

```csharp
public sealed record Line(
    string Id,
    string? Speaker,                 // actor ID; null for narration and options
    string? SpeakerName,             // the actor's display name
    string Text,                     // plain text, fully resolved; an icon is U+FFFC, {br} is \n
    ImmutableArray<Span> Spans,      // (Name, Start, Length, Arguments)
    ImmutableArray<Marker> Markers,  // closed set of records, each with a Position
    ImmutableArray<Icon> Icons,      // (Position, Name), one for each U+FFFC in Text
    TagCollection Tags);
```

Rendering a template resolves the interpolation and conditionals against the state in one pass, producing the text and everything positioned in it. Everything is evaluated before the line is returned, so a failing host function leaves the runner where it was.

- **Positions are UTF-16 indices** into `Text`, in logical order (which is what .NET strings use), after the text is trimmed. Adapters convert them if their engine counts differently. A span's `Length` counts the same way, and a span can be empty (`[b][/b]`).
- **Spans** come from `[name args]…[/name]`, including the built-ins `b`, `i`, `u`, `s` and `color`. They are ordered by start, with an outer span before an inner one that starts at the same place. A span's `Arguments` are read by parameter name, like a command's, with defaults filled in.
- **Markers** are records under `Marker`, at the position where they appear, in source order when several share one. Hosts and `LineReveal` match on them and keep a default arm, since later versions add kinds.

  | Source | Marker | Payload |
  | --- | --- | --- |
  | `{w}` | `InputWaitMarker` | |
  | `{w d}` | `PauseMarker` | `TimeSpan`, clamped like host function durations |
  | `{p}` | `PageBreakMarker` | |
  | `{speed x}`, `{speed}` | `SpeedMarker` | The factor in effect after the marker: `x`, or 1 for `{speed}`. Speed resets to 1 at the start of every line, with no marker. |
  | `{@command args}` | `CommandMarker` | A `CommandInvocation` and `Waits`, resolved like a statement command's |

- **A `{w d}` or `{speed x}` whose value isn't more than zero** produces no marker and a `NonPositivePause` or `NonPositiveSpeed` [warning](#runtime-warnings). Constants are rejected when the story is checked (PIB2039); this covers values computed at run time.
- **Only the chosen branch** of conditional text contributes spans, markers and icons. Inline commands fire again when a line is rendered again after a load, which is why only commands declared `inline` can appear in text.
- **Icons** are in their own array, not among the markers: markers are things the reveal does, and an icon is neither timing nor an effect. A literal U+FFFC can't be written in a story (PIB1048), and one in a shown value shows as U+FFFD, so the character in `Text` always means an icon.
- **Option text** is a `Line` too, with spans, icons and tags. It can't hold commands, `{w}`, `{p}` or `{speed}`, so its markers are always empty.
- **Trimming.** The rendered text is trimmed of leading and trailing whitespace, whatever it came from: an `{if}` that renders nothing, a shown value's own spaces, a `{br}` at either end. Spaces inside the text stay. Every position is computed after trimming, and a marker, icon or span edge that fell in trimmed space moves to the nearest end of the text.
- **Shown values** are inserted literally and never read as markup. A number uses the invariant culture, with no digit grouping and no trailing zeros (`1.50` shows `1.5`), and an actor shows its display name. Localized formatting comes with localization.
- **Lines compare by value:** two lines are equal when all their fields are, including the contents of the arrays, and so are spans, markers, icons and tags.
- **Tags** reach the host in source order, `#id` included, as `Tag(Name, Kind, Value)`: `Kind` is `Flag`, `Text`, `Enum` or `Reserved`, and an empty optional value is `null`. `line.Tags` also gives typed access: `Has("thought")`, `GetString("cue")`, `GetEnum("mood")` and `GetEnum<TMood>("mood")` (the generic `Enum.TryParse`, no reflection). Asking for a tag the story doesn't declare throws `ArgumentException`, and so does the wrong accessor for its kind (`InvalidOperationException`); asking for a declared tag the line doesn't have isn't misuse: `Has` is false and the getters return `null`.
- **Host enums.** `story.ValidateEnum<TMood>("mood")` compares a C# enum's members with a story enum's and returns the mismatches without throwing, so a renamed member fails when the game starts, not mid-scene. `story.Nodes` lists each node's current name and its `#was:` names.

### The reveal helper

```csharp
var reveal = new LineReveal(line, new RevealSettings(CharactersPerSecond: 40, Instant: false));

RevealFrame frame = reveal.Advance(delta);   // what's visible, which markers were reached, the state
reveal.Skip();                               // jump to the next stop point; fires skipped effect markers
reveal.Resume();                             // continue after an input wait, page break or blocking command
```

`LineReveal` is optional: a host can read a line's markers itself. It is pure logic with no engine, so every host reveals text the same way, and the Godot adapter's reveal label drives it by setting its visible character count each frame. `Skip` and `Resume` return a `RevealFrame` too.

- **The frame** is `RevealFrame(State, PageStart, VisibleLength, Fired)`. The text on screen is `line.Text[PageStart..VisibleLength]`, and `Line.Text` itself is always the whole line. `Fired` is the markers the reveal reached during that call, in order. Frames compare by value.
- **States:** `Revealing`, `WaitingForInput` (after `{w}` or `{p}`), `WaitingForHost` (after a blocking inline command, until `Resume()`), `Complete`.
- **Waiting.** `Advance` and `Skip` while the reveal waits or is complete return the unchanged frame, with nothing fired, so a host can call `Advance` every frame. The time that passes while it waits is dropped, and so is any time left over when the reveal reaches a stop, so a long frame never makes the text burst out after `Resume()`. `Resume()` when nothing waits is misuse and throws `InvalidOperationException`.
- **Reveals move by grapheme cluster** (`StringInfo`), so emoji and combining marks never show half-drawn. `VisibleLength` is a UTF-16 index into `Line.Text`, like every position, and always falls on a cluster boundary. Segmentation follows the runtime's Unicode version, so .NET 8 and .NET 10 can split the newest emoji differently; reveal tests and properties avoid emoji newer than Unicode 15.
- **Timing.** Each cluster takes `1 / (CharactersPerSecond × factor)` seconds, where the factor is 1 until a `SpeedMarker` sets it. The factor is relative to the player's setting, and an inner speed replaces an outer one: there is no nesting. A `\n` takes no time, an icon takes one character's time, and spaces are timed like any other character. A `PauseMarker` holds the reveal for its duration. There are no extra pauses after punctuation. `CharactersPerSecond` is a `decimal` and must be more than zero (`ArgumentOutOfRangeException` otherwise), and `Advance` throws for a negative time.
- **Time is exact.** The reveal counts ticks in `decimal`, never `double`, so frames depend only on the line, the settings and the sequence of calls, and the same total time reaches the same place however it is split into frames.
- **`Instant`** shows everything up to the next stop at once. It drops timing (pauses, speed) but still stops at input waits, page breaks and blocking commands, and still fires effect markers.
- **Markers fire in order** when the reveal reaches their position, before the character at that index shows. Several at one position keep their order, and a pause holds the reveal before the markers after it fire. Markers at position 0 fire on the first `Advance`, and markers at the end fire before the reveal can be `Complete`. A marker inside a grapheme cluster fires just before the cluster shows.
- **Which markers `Fired` reports.** In normal play, every marker reached: pauses, speed changes, input waits, page breaks and commands. While skipping, and in `Instant` mode, only the commands, input waits and page breaks: the pauses and speed changes are dropped. Skipping still applies speed changes, so timing resumes at the right speed.
- **Commands are the effects,** and the host carries them out. One whose `Waits` is false fires and the reveal carries on. One whose `Waits` is true fires and the reveal enters `WaitingForHost` until `Resume()`.
- **`Skip`** jumps to the next stop point: an input wait, a page break, a blocking command, or the end. It fires every effect marker it passes, in order, and drops the timing, including the rest of a pause already under way. Skipping never changes the outcome: every effect still happens, in order ([reference](language/reference.md#runtime-semantics-summary)).
- **Page breaks clear the box.** `{p}` stops the reveal; after `Resume()` the frame's `PageStart` is set past any spaces and line breaks that follow the break, but never past the next marker (so a marker there is at or after `PageStart`, not hidden before it), and the host shows only the text from there on.

## State and saves

`StoryState` holds everything that belongs in a save slot:

- `@var` values
- Node visit counts
- Variation block entry counts (by block ID)
- Options already chosen (by option ID), for `WasChosen` and `@once`
- Current pose of each actor
- The seed

State that outlives a save slot, such as unlocks or read tracking, belongs to the game ([boundaries](boundaries.md)).

```csharp
// Saving: reach a save point, carrying out the steps passed as skip mode would.
foreach (DialogueStep step in runner.FastForward()) { /* finish instantly */ }
var (stateSnapshot, stateProblems) = state.CreateSnapshot();
var (runnerSnapshot, runnerProblems) = runner.CreateSnapshot();
string stateJson = SnapshotJson.Serialize(stateSnapshot);       // stored however the host likes
string runnerJson = SnapshotJson.Serialize(runnerSnapshot);

// Loading, against the story as it is now.
var (restoredState, stateLoadProblems) = StoryState.Restore(story, SnapshotJson.DeserializeState(stateJson));
var (restoredRunner, runnerLoadProblems) = DialogueRunner.Restore(story, restoredState, functions, SnapshotJson.DeserializeRunner(runnerJson));
// then restoredRunner.Next() as usual
```

- **Snapshots are plain public records.** `state.CreateSnapshot()` gives a `StateSnapshot`, and `StoryState.Restore(story, snapshot)` makes a new state from one. Each returns a `SaveResult<T>`: the snapshot or the restored state, and a list of `SaveProblem`s ([below](#the-report)). A record deconstructs, so `var (snapshot, problems) = …` reads both.
- **Keys are chosen to survive script edits.**

  | State | Keyed by | Why |
  | --- | --- | --- |
  | Variables | Name | Survives reordering declarations |
  | Visit counts | The node's name | On restore, a saved name resolves through `#was:` aliases, so a renamed node keeps its count |
  | Variation block entry counts | Block ID | A block keeps its count when blocks are inserted, reordered or moved to another node |
  | Chosen options | Option ID | `WasChosen` and `@once` removal survive rewording and reordering |
  | Poses | Actor ID, to the pose's name | |
  | Seed | | Stored, unused in v1 |

- **Values are typed and exact.** A `SavedValue` holds the variable's type as the story writes it (`number`, `mood`) and one of `Bool`, `Number` (a number, or a duration in seconds, as its `decimal`) and `Text` (a string as written, or an enum member, actor or node by name).
- **Variation blocks store an entry count, not a position.** Each block records how many times execution has entered it, and its kind derives what to run from that count ([reference](language/reference.md#variations)). A saved count therefore has a defined meaning whatever happens to the block afterwards: alternatives added or removed, or its kind changed. There's nothing to convert.
- **Fallback IDs are never saved.** A line, option, call or block without an `#id` has an ID the compiler made up, which changes when the file is edited ([localization design](localization.md#when-theyre-required)), so saving it could hand one line's state to another. A snapshot leaves out a chosen option or block entry count keyed by one, and reports it. This only happens in development, since `pibbles check --warnaserror` rejects missing IDs.
- **Restoring drops what no longer fits, and reports it,** so an old save still loads after a patch. It never throws for content: a variable that no longer exists or whose type changed (it keeps its starting value), a value that names something that's gone (an enum member, actor or node), visit counts for a node that no longer exists under any name, entry counts for a block that's gone, a chosen option that's gone, and a pose for an actor or pose that's gone. Variables the snapshot doesn't have get their starting values.
- **Two saved names can land on one node,** when a node takes the other's name as a `#was:` alias. Its visit count is their sum, since both counted entries to what is now that node, and the report says so.
- **Snapshots carry a format version,** `Format`, which says what the snapshot holds and is about Pibbles, not the story. Both snapshots start at format 1 (`StateSnapshot.CurrentFormat`, `RunnerSnapshot.CurrentFormat`), separately. The language grows by [extensions](language/design.md#grammar-extensions) that add state ([below](#with-extensions)), so a newer core restores every older format, filling new state with its defaults. A format newer than the core reads, which means the game was downgraded, is host misuse and throws `ArgumentException`.
- **JSON.** `SnapshotJson.Serialize` writes either snapshot, and `SnapshotJson.DeserializeState` and `DeserializeRunner` read them, throwing `JsonException` for JSON that isn't one. They're built on `System.Text.Json` source generation, which is part of the BCL and AOT-safe, and they keep every digit of a `decimal`. The JSON is a save-file format, so a test pins its shape. The host decides where snapshots are stored.

### Saving mid-dialogue

Hosts save during dialogue, so the position of a running dialogue is part of a save too. The design rests on one rule: **every position a save can point to has an ID,** so restoring is an exact lookup, never a guess.

#### Save points

- **A dialogue's position is always a line or a choice.** Those are the moments it waits on the player.
- **`runner.CreateSnapshot()`** can be taken right after `Next()` returned a `LineStep` (the host is showing that line), while a choice is waiting, or when no dialogue is running (never started, or ended), which gives `RunnerSnapshot.Empty`. At any other moment, just after a `CommandStep`, `PoseStep` or `WaitStep`, or between `Start` or `Choose` and the next `Next()`, it throws `InvalidOperationException`, saying to fast-forward first. The runner remembers the ID of the line it last delivered, because its own position has already moved past the line.
- **The player can still save at any time,** because the host **fast-forwards** first. `runner.FastForward()` calls `Next()` until it gets a `LineStep`, a `ChoiceStep` or an `EndStep`, and returns every step it passed, in order, the last one included. The host carries out the passed commands instantly (a command handler's skip variant), applies the poses, drops the waits, and shows the final line or choice, then takes the snapshot. This is the same mechanism as [skip mode](godot.md#skipping), so it adds nothing new for the host. If the dialogue ends first, the save has no dialogue in progress. At a save point already, `FastForward()` returns an empty list and changes nothing, so a host can always call it before saving. The instruction budget covers the whole fast-forward, so a story that loops forever giving commands still ends with an `InfiniteLoop` warning instead of hanging the game.
- **The runner snapshot is separate from the state snapshot,** because several runners can share one state. The host stores both in its save file. A `RunnerSnapshot` holds the format version and the call stack as IDs: `Calls`, the IDs of the `@call`s the dialogue is inside, outermost first, since calls carry IDs ([localization design](localization.md#what-needs-an-id)); and either `Line`, the ID of the line, or `Choice`, the IDs of all its options in source order, including any that were hidden, unavailable or already used up. A choice has no ID of its own, so the whole set identifies it, and any one survivor is enough to find it again. `HasDialogue` says whether it has either.
- **A dialogue waiting on a fallback ID saves as no dialogue in progress,** and the report says so: a line or a call whose ID is a fallback, or a choice whose option IDs all are. A choice that has some options with real IDs saves those.
- **Loading:** `DialogueRunner.Restore(story, state, functions, snapshot, options)`, followed by `Next()` as usual.
  - A line resumes at its `Line` instruction, so the next `Next()` shows it again, rendered afresh. Text that depends on host functions can differ, since the game's own state may have changed. The `Pose` before a posed line isn't replayed: the host redraws poses from `StoryState.GetPose`. The reveal starts over and the line's inline command markers fire again, which is why only commands declared `inline` may appear in text ([reference](language/reference.md#commands)). With inline variations, the line is rendered without bumping its show count, so it picks the same alternatives as before.
  - A choice resumes at its `Choice` instruction, so the next `Next()` evaluates its options' conditions again against the current state. If none is available, it's skipped like any other choice, and the report says so. `Restore` checks this as it loads, so a host function that fails there throws `HostFunctionException`, as it would from `Next()`.
  - A call frame resumes at the instruction after its `@call`.
  - **Visits never count on restore.**
  - With no dialogue in progress, the restored runner is ended: `Next()` returns `EndStep` until `Start`.

#### What the host saves

- **The host restores what's on screen.** Pibbles restores what it owns: variables and poses. Which actors are staged where, the background and the music are host state, and the host saves them alongside. The host already has this state in its stage manager, so serializing it there is simpler than having Pibbles rebuild the stage.
- **The host saves its intended state, not animation in progress.** A stage manager that records each command's target when the command is issued is already consistent after a fast-forward.
- **Long gameplay handoffs aren't blocking commands.** Fast-forwarding would skip a command that waits for a whole minigame. Handoffs like that end the dialogue and give a continuation node instead (`@enter_room kitchen on_exit=kitchen.leave`), and the game saves its own gameplay state.

#### Restoring after an update

A saved ID still exists after an update as long as the line keeps its `#id`, however it was reworded or wherever it moved. The dialogue resumes there, and continues through the flow of the node it's in now.

**A choice is found by any of its saved option IDs** that is still an option (an ID that now belongs to a line doesn't count), and the whole choice is shown again, so deleting or reordering options never loses a save. If the surviving IDs now belong to different choices, because an edit split the options, the dialogue resumes at the choice holding the most of them, the earliest in saved order on a tie, and the report says so.

**A frame whose ID no longer exists is lost,** and the report says which. Every lost frame is dropped. A lost call frame comes off the call stack, so the dialogue won't return there. If the line or choice is lost, the dialogue resumes as if its node had returned to the nearest surviving call frame, at the instruction after that `@call`. If no frame survives, there's no dialogue in progress. In v1 this happens whenever an update deletes a line, or all of a choice's options, that a save is waiting on. Keep a replaced line's ID on its replacement rather than deleting the line. [Aliases and migrations](#release-manifests-and-migrations) give retired IDs somewhere to go later.

**Nothing is guessed.** If the right place to resume isn't obvious, the report says so and the dialogue doesn't resume there.

#### The report

Taking and restoring snapshots report what they couldn't carry over as a list of `SaveProblem`s, the same shape as `HostFunctions.Validate`'s problems: a `Kind`, the `Subject` (the saved ID or name), and a `Message` for the game's developers that names it and says what was done instead. The kinds can grow, so a host that switches on them keeps a default arm.

| `SaveProblemKind` | Reported by | What was done |
| --- | --- | --- |
| `FallbackId` | Either `CreateSnapshot` | State or a position keyed by a made-up ID was left out; the message suggests `pibbles ids` |
| `UnknownVariable` | `StoryState.Restore` | The variable no longer exists; its value is dropped |
| `VariableTypeChanged` | `StoryState.Restore` | The variable keeps its starting value |
| `InvalidValue` | `StoryState.Restore` | A value no longer fits its type, such as a removed enum member, and the variable keeps its starting value; or a count isn't a count, and it's dropped |
| `UnknownNode` | `StoryState.Restore` | Visits to a node that's gone under every name are dropped |
| `VisitsMerged` | `StoryState.Restore` | Two saved names are now one node; its count is their sum |
| `UnknownBlock` | `StoryState.Restore` | Entries to a block that's gone are dropped |
| `UnknownOption` | `StoryState.Restore` | A chosen option that's gone is dropped |
| `UnknownActor`, `UnknownPose` | `StoryState.Restore` | The pose is dropped, and the actor keeps its default pose |
| `LostFrame` | `DialogueRunner.Restore` | The frame is dropped, and the dialogue unwinds |
| `ChoiceSplit` | `DialogueRunner.Restore` | The dialogue resumes at the choice with the most saved options |
| `NoOptionAvailable` | `DialogueRunner.Restore` | The restored choice is skipped |

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

Migrations scoped to a release need to know which release a save came from, so release manifests add the story's version (from `pibbles.json`) to the runner snapshot, as a new field in a new snapshot format. v1 has no story version, because restoring finds everything by ID and a version would only be reported, never acted on.

A choice frame tries each step on all of its option IDs, in saved order, before moving on to the next step. So a surviving option always wins over an alias or a migration for one of its removed siblings. Variables also follow `#was:` aliases on `@var`, and line state follows line `#was:` aliases.

The **save across releases** property tests this: record a release, make random edits until `pibbles check` passes against it, then restore saves from every shipped position. Each one lands exactly where its ID, alias or migration says, and never throws.
