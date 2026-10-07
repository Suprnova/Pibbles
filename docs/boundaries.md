# Boundaries

This document draws the line between Pibbles and the host (the game). Getting it right keeps the core small and engine-agnostic without pushing work onto every host that it would have to redo.

## The rule

A feature belongs to Pibbles if it answers **what** content runs or **when** something happens relative to the story and its text. It belongs to the host if it answers **how** something looks, sounds or feels, or if it needs to know about the engine: pixels, audio, input devices, files or frame timing.

One refinement: **pure logic that every host would otherwise reimplement the same way ships in the core as an optional helper**, even when it serves presentation. The reveal timeline ([below](#the-reveal-helper)) is the main example.

Pibbles plays three roles in each feature:

- **Defines:** the language has syntax for it.
- **Validates:** the analyzer checks it against the declarations.
- **Schedules:** the runtime decides when it happens and hands it to the host.

The host **implements** it.

## Feature by feature

| Feature | Pibbles | Host |
| --- | --- | --- |
| Parsing, validating, running scripts | Everything | Supplies source files |
| Text color and formatting | Parses `[markup]` into styled spans with positions. Validates tag names and arguments. Ships `b i u s color` in a prelude. | Renders spans, for example as Godot BBCode |
| Text effects (wave, shake, rainbow) | Host-declared markup, validated and delivered as spans | Implements the effect, for example with a `RichTextEffect` |
| Recorded voice lines | Gives each line a stable ID | Maps the line ID to a clip. Plays it, and stops it on advance. |
| Voice barks (sighs, grunts) | A host-declared inline command (`{@bark sigh mira}`) | Plays a per-actor clip on the voice bus |
| Audio in the middle of a line | `{@sfx thud}` marks an exact position. The reveal helper fires it. | Plays the sound |
| Inline images and controller glyphs | `{icon interact}`: a declared, one-character inline object | Resolves the icon for the current input device and renders it |
| Custom fonts | Nothing | Everything |
| Conditional text and branches | Everything | Nothing |
| Actors | Identity, display name, declared poses, current pose of each actor | Sprites, nameplates, colors, voice blips |
| Placement and staging | Host-declared commands with typed arguments (`@show mira left`, where `left` is a declared enum value) | Positions, layout, transitions |
| Pose / sprite swaps | `mira (smug):` changes a pose. Pibbles validates the pose against the actor and tracks it. | Swaps textures and animates the swap |
| Sprite animations (bounce, jolt) | Host-declared commands, run as a statement or (when declared `inline`) at a position in a line, with wait semantics | Implements the animation |
| Screen effects | Same as sprite animations | Same as sprite animations |
| Text speed | `{speed 0.5}` sets a multiplier relative to the player's setting, until `{speed}` or the end of the line. The reveal helper does the timing. | Player speed setting, rendering visible characters |
| Pauses and input waits | `{w 0.5}`, `{w}` and `@wait 1s` produce timing markers and steps | Drives the clock and input |
| Newlines and page breaks | `{br}` and `{p}` produce markers | Renders them. Paginates text that overflows, since only the host knows box size and font. |
| Choices | Which options exist, whether each is available, which have been chosen before, `@once`, where flow goes next | Choice UI. Whether unavailable or already-chosen options are hidden or shown greyed out (based on option tags and flags). Choice timers. |
| Variables and flags | Declared, type-checked variables for facts the story decides, stored in a serializable state | Reads and writes variables from game code, and persists the state. Owns facts from gameplay and anything that outlives a save slot (unlocks), exposed through functions and commands. |
| Game events (camera, switching to other gameplay) | Host-declared commands. Typed parameters include `node`, so continuations are validated. | Implements them |
| Puzzle and inventory state | Calls host functions (`has_item("key")`) declared with signatures | Implements the functions |
| Entry points from gameplay | Nodes are addressable by name. `#was:` aliases keep old names working after a rename. | Decides which node to start, and when |
| Save and load | Serializable snapshots of the story state and of a dialogue's position (always a line or choice, fast-forwarding to one if needed), keyed by IDs and names that survive edits | Storage and save slots. Saving and restoring its own stage state (who is shown where, background, music), as intended end states rather than animation in progress. |
| Skip mode | Skipping never changes the outcome: every state change, command and effect still happens, and only timing is dropped. The adapter's runner node provides the skip mechanism. | When skipping starts and stops (seen text only, stop at choices), the skip UI, and an instant, correct end state for every command while skipping |
| Read tracking (skip read text) | Every line carries a stable ID | Keeps the set of seen line IDs, outside save slots |
| Auto mode | Reveal durations and markers | Auto-advance timing policy |
| Backlog / history | `Line` objects carry everything a backlog needs | Stores and displays them |
| Text boxes, thought bubbles, phone messages | Declared line tags (`#thought`), validated and passed through as-is | Chooses the presentation |
| Typewriter reveal | The optional `LineReveal` helper (pure timing logic) | Shows the visible characters it reports |

## Justifications for the less obvious calls

### Why markup is spans, not BBCode

The core outputs plain text plus a list of `(tag, arguments, start, end)` spans and positioned markers. The Godot adapter turns that into BBCode in about a page of code. If the core emitted BBCode, it would tie itself to one engine's dialect, and the CLI, tests and any other engine would have to parse it back apart. Spans are also exactly what a backlog, a text-to-speech pass or a test assertion needs.

### Why semantic markup is encouraged over raw colors

`[color "#ff8800"]` works (it's in the prelude), but `[clue]` is better. The game can restyle every clue in one place, and colorblind modes are a host setting instead of a script rewrite. Once localization arrives, `clue` can also be marked `required`, so no translation loses a gameplay hint. Raw colors are for one-offs.

### Why actors and poses are first-class but staging is not

Actors show up on almost every line, both as speaker and pose. Making them first-class buys three things. Writers get compact syntax (`mira (smug): …`). The analyzer can check a pose against that actor's declared poses. And the runtime can track each actor's current pose, so a save made mid-scene restores faces correctly.

Staging (positions, entrances, exits, transitions) varies a lot from game to game. Host-declared commands with typed arguments (`@show mira left`) get the same validation without Pibbles having to model a stage. If a second game ever needs a richer stage model, it can be added then.

### Why animations and effects are declared commands, not built-ins

Any built-in list (`bounce`, `shake`, `jolt`) would be both too long and too short: every game wants different effects with different parameters. A declared command is validated just as strictly as a built-in would be, and implementing it is the host's job anyway. What Pibbles does own is the *scheduling*: running at a statement or at an exact character, and whether the story waits for it. The declaration also says whether a command may be used inside text at all (`inline`), because an in-line command can fire again when a saved line is replayed.

### Why icons are special and not just markup

A controller glyph takes up space in the text, carries information the player needs ("press ⓐ"), and has to be resolved at display time because the player may switch from keyboard to gamepad mid-line. Treating it as a one-character inline object (U+FFFC in the plain text) keeps positions correct for the reveal helper. Once localization arrives, translations must keep it too.

### Why pagination is the host's job, but `{p}` exists

Only the host knows the text box size and the font, so automatic pagination of overflowing text has to live in the host. `{p}` is the writer's (or translator's) way to force a page break at a meaningful spot. German runs about 30% longer than English, so translators need this power without changing the line structure.

### Why variables live in Pibbles but persistence doesn't

The story has to type-check and evaluate conditions, so it owns the variable model. Where and how saves are written (slots, cloud sync, encryption) is the game's business, so Pibbles produces a snapshot and stops there.

### The reveal helper

Showing a line one character at a time and firing its markers (pauses, speed changes, input waits, inline commands, auto-advance) is pure logic. It doesn't depend on any engine. Every host would reimplement it, and subtle differences would creep in. Does a skipped reveal still fire its sound effects? Does a pause count toward auto-mode time? Putting `LineReveal` in the core gives:

- **One definition of the timing semantics,** shared by the game, the CLI player and tests.
- **Deterministic tests** of pacing without an engine.
- **A thin adapter,** which only sets the label's visible character count each frame.

It is optional. A host that wants different behavior can read the markers itself.

## The contract goes both ways

Declarations are a contract between writers and programmers:

- **Writers** fix what the analyzer reports. It checks that scripts only use what is declared, with the right types.
- **The game developer** implements and registers a handler for every declared command, markup, icon and function, and keeps story names used in game code (nodes, actors, poses) in step with the story. Keeping declarations in predictable places, such as `defs.pib` or `story/commands/`, makes changes easy to spot in review, and a test that calls the registries' `Validate` catches a missing handler before it ships. Their CI runs `pibbles check --release --warnaserror`. Failures reported in release builds are theirs to handle.
- **Pibbles** catches every mismatch it can, as early as it can: in the editor, in the CLI, at export and on launch. It gives every failure a safe fallback, so story content never crashes or hangs the game ([Godot design](godot.md#failures)).

## What the host always provides

To run a story, a host provides:

1. The source files (or a compiled story, once that format exists).
2. Implementations of the declared host functions, registered as delegates.
3. A handler for each kind of step: lines, choices, commands, pose changes, waits and the end.
4. Persistence for the state and runner snapshots, together with its own stage state, if the game saves.
5. The current locale, once the game is localized ([below](#with-extensions)).

## With extensions

What the [grammar extensions](language/design.md#grammar-extensions) and later tiers ([roadmap](roadmap.md#feature-tiers)) add. Each row joins the table above when its feature ships.

| Feature | Arrives with | Pibbles | Host |
| --- | --- | --- | --- |
| Localization | Localization pipeline | String extraction, translation validation, looking up the right locale's text at runtime, fallback. Translations must keep icons and `required` markup. Actor display names are localized. | Chooses the locale, fonts, bidi shaping, and UI strings outside the story |
| Recorded voice lines | Localization, voice tooling | Clips are keyed by locale too. `#voice:` points a line at another line's recording, and `#unvoiced` excludes one. Recordings are tracked for staleness. | Plays the clip for the current locale |
| Voice barks | `speaker` parameter defaults | `{@bark sigh}` defaults to the speaker | Plays barks in the dub language |
| Mystery characters and disguises | Personas | Which persona is active, worked out from state, with a localized name override | Silhouettes, alternate sprites, masked voices, keyed by persona |
| Pose changes partway through a line | Mid-line poses | `{(sad)}` and `{rex (sad)}` change a pose at a point in the line | Swaps the sprite when the reveal reaches it |
| Auto-advance | `{auto}` | `{auto}` produces a marker | Advances without waiting for input |
| Randomness | `@shuffle` and `random()` | Seeded generator kept in the state | Chooses the seed for a new game |
| Save and load across releases | Release manifests and migrations | Release checks guarantee old saves still resolve after updates | Nothing |
| Hot reload while developing | Hot reload (v1.x) | Recompiles changed sources | Watches files and decides what to restart |
