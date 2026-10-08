# Godot design

`Pibbles.Godot` is a generic engine adapter in this repository. It knows nothing about any particular game. It targets `net8.0` and `net10.0`, so any Godot 4.4+ project can use it ([architecture](architecture.md#target-frameworks)).

## What the adapter contains

- **Loading:** reads `.pib` files through `FileAccess` from a configured `res://` folder, compiles them at startup, and reports diagnostics ([failures](#failures)). Export presets include `*.pib` in their non-resource file filters.
- **A runner node:** a `Node` wrapping `DialogueRunner` that exposes steps as signals and supports `await` for blocking commands.
- **An export plugin:** compiles the story whenever the game is exported, and fails the export on any error, so a story with errors can't ship.
- **Command and markup registries:** typed delegate registration. `Validate(story)` lists every declared command, markup, icon and function that has no handler. The adapter calls it on launch, and the game's tests can call it too ([boundaries](boundaries.md#the-contract-goes-both-ways)).
- **Text rendering:** `Line` → BBCode. It escapes `[` in the text, emits spans as tags (built-ins to BBCode, custom markup to `RichTextEffect` tags), and emits icons as `[img]` resolved through an icon provider that knows the current input device.
- **A reveal label:** a `RichTextLabel` subclass driven by `LineReveal` through `visible_characters`.
- **Save support:** bundles the state and runner snapshots together ([saving mid-dialogue](runtime.md#saving-mid-dialogue)).
- **Skip mode:** see [below](#skipping).

## Skipping

Skip mode races through a dialogue, and the save fast-forward is skip mode that stops at the next line or choice. The adapter doesn't step the runner itself for a save: it calls the core's `DialogueRunner.FastForward()`, carries out the steps it returns as it does while skipping, then takes the snapshots ([runtime design](runtime.md#save-points)). If a host function fails partway, `FastForward()` throws a `FastForwardException`; the adapter carries out the steps in its `Passed` first, then handles the failure as it does any other ([below](#failures)). The work is split three ways:

- **The core defines what skipping may drop:** timing only. Every `@set`, command and effect marker still runs, in order ([reference](language/reference.md#runtime-semantics-summary)), so skipping can never cause a missed `@set`. Every `LineStep` carries its line ID, which is all read tracking needs.
- **The adapter provides the mechanism.** While the runner node's `Skipping` flag is on, it reveals lines instantly, passes input waits, drops `WaitStep`s, and doesn't wait on blocking commands beyond their handlers' instant finish. Command handlers receive a context with `IsSkipping`, so a handler can use a skip variant of its effect: place the sprite at its target, cut the animation, mute the sound. The game passes a predicate that decides whether a line may be skipped, and skipping stops at the first line it rejects and at every choice.
- **The game decides the policy and the presentation:** the skip UI and input, the predicate (`line => seenLines.Contains(line.Id)` for seen text only, `_ => true` for everything), and the set of seen line IDs, stored with the player's profile rather than a save slot. Every command must reach its intended end state instantly while skipping. That isn't optional, because saving relies on it.

## Failures

Story content never crashes or hangs the game. Debug builds report problems as loudly as possible, and release builds fall back and keep going.

| Failure | Debug build | Release build |
| --- | --- | --- |
| The story has compile errors | Every diagnostic is reported, and no dialogue runs | Can't happen, because the export plugin fails the export |
| A declared command, markup, icon or function has no handler | Every missing handler is reported on launch. Each use reports again and falls back. | Each use falls back and reports |
| The game starts a node that doesn't exist | Reported with the closest name, and the dialogue ends | Same |

- **A missing handler doesn't stop a debug build,** because declaring a command before it's implemented is the normal workflow. The launch report makes the gap visible, and the game's tests keep it from shipping ([boundaries](boundaries.md#the-contract-goes-both-ways)).
- **Fallbacks:** an unimplemented command is skipped, and a blocking one finishes at once. Unimplemented markup renders its text unstyled. A missing icon renders nothing. A missing function returns its return type's default (`false`, `0`, `""`). The adapter registers these fallbacks as stand-ins before the runner starts, so the core stays strict.
- **The dialogue always ends.** Any failure the runner node can't recover from ends the dialogue, so game code waiting for it to finish never hangs.
- **Reporting:** every failure goes to `GD.PushError` and to the adapter's `ErrorReported` signal, which the game can forward to telemetry.

## The game's side

Everything specific to one game lives in that game's repository, in its own narrative layer on top of the adapter:

- The command, markup and icon handlers.
- The stage manager, which knows who is on screen and where, and saves that state.
- The dialogue box scene, built on the adapter's runner node and reveal label.
- The declarations in `story/`, since they describe that game's contract.

**Referencing Pibbles:** a game developed alongside Pibbles can use a `ProjectReference` to a sibling checkout (`../Pibbles/src/Pibbles.Godot/Pibbles.Godot.csproj`), so a change in Pibbles shows up in the game on the next build. Otherwise it uses a NuGet `PackageReference`.

## With extensions

What the [grammar extensions](language/design.md#grammar-extensions) and later tiers ([roadmap](roadmap.md#feature-tiers)) add to the adapter.

### Drafts

The adapter compiles debug builds with the development profile and exported builds with the release profile, so [drafts](language/design.md#drafts) never ship even if their files do ([runtime design](runtime.md#drafts)). Excluding the drafts folder in the export preset keeps them out of the package as well.

### Localization

The adapter also loads `.po` files (add `*.po` to the export filter) and follows `TranslationServer.GetLocale()` ([localization design](localization.md)).

### Hot reload (v1.x)

The Godot adapter watches the story folder in the editor and recompiles on change.

### Story names in scenes (stretch)

Games often name story nodes, actors and poses on scene objects rather than in code: a door whose `StartNode` is `kitchen.door`, or a portrait table keyed by pose. These are plain strings, so a typo or a writer's rename only shows up when someone clicks the door. With the story compiled in the editor (the same work as hot reload), the adapter's editor plugin checks them:

- **A picker.** The game marks an exported string with a hint, such as `[Export(PropertyHint.None, "pibbles:node")]`, and an `EditorInspectorPlugin` replaces the text box with a searchable list of the story's nodes. `pibbles:actor` and `pibbles:pose` work the same way.
- **Warnings in the scene tree.** A helper that game scripts call from `_GetConfigurationWarnings` shows Godot's warning icon on an object whose marked property names something that no longer exists.
- **An export check.** The export plugin scans scenes and resources for marked properties, and fails the export if one doesn't resolve.

Names written as string literals in C# aren't covered. Keeping those in step with the story is the game developer's job.
