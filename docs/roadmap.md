# Roadmap

## Feature tiers

**Core (v1)** is what a game needs before content production can scale. Its language is the [language reference](language/reference.md), and writers learn it from the [writer's guide](language/guide.md).

- Declarations: actors with poses, enums, variables, commands (with `inline` and `waits`), markup, icons, tags, functions
- Nodes with tags, `#was:` aliases, and `@prefix`. `@jump`, `@call`, `@return`, `@end`, `@wait`
- Text lines with speaker, pose and narration. Pose-only lines
- Choices with `@if`, `@once`, tags and gather
- `@if`/`@elif`/`@else`, `@set`, expressions, `visits()`
- Block variations: `@sequence`, `@cycle`, `@once`
- Inline: markup spans, `[speed]`, interpolation, inline commands (with `wait`), `{w}`, `{p}`, `{br}`, `{icon}`, `{if}`
- Line IDs (syntax, checks, `pibbles ids`)
- Runtime: runner, state and versioned snapshots, mid-dialogue saves (ID-based positions, fast-forward to save points), `Line` rendering, `LineReveal`
- CLI: `check`, `play`, `ids`
- TextMate grammar
- Godot adapter

**Later (v1.x)** is planned and designed, and follows once the core is proven.

- Localization pipeline: PO extract and update, translation validation, runtime locales, fallback. `required` markup and `///` notes arrive with it.
- Language server and VS Code language client, with spell checking
- Hot reload in the Godot editor

**Grammar extensions** are designed in the [language design](language/design.md#grammar-extensions), each with the trigger that brings it into v1.

**Stretch** is nice to have. Each item waits for a concrete need.

- `{plural}` with CLDR rules, and bidi isolation of interpolated values
- Voice tooling: `#voice` and `#unvoiced`, recording scripts, and the recording manifest with staleness checks; `pibbles graph`
- A path explorer that walks every branch to report reachability and coverage, and finds dead ends
- Story names in the Godot inspector: a picker, scene-tree warnings and an export check ([Godot design](godot.md#story-names-in-scenes-stretch))
- A precompiled story format
- Coverage-guided fuzzing (SharpFuzz)
- A formatter (needs a lossless syntax tree)
- A command journal that lets a host rebuild its stage by replaying staging commands, if saving stage state by hand turns out painful

**Non-goals** are covered in the [language design](language/design.md#non-goals).

## Phases

Each phase ends with something usable and a clear exit check.

### Phase 0: Scaffolding

- `docs/`: the design documents, the language reference and the writer's guide, with `docs/language/examples.pib` as the documentation prelude ([architecture](architecture.md#2-completeness-gates)).
- The solution: `Pibbles.slnx`, `Directory.Build.props`, `global.json`, the core, the CLI and the test project.
- `AGENTS.md`.
- `samples/kitchen`.

**Exit:** `dotnet build` and `dotnet test` pass on the skeleton, and AGENTS.md is in place.

### Phase 1: Syntax

- Line classifier, statement parser, inline parser, syntax tree, diagnostic infrastructure and catalog.
- `pibbles check` reports syntax errors.
- TextMate grammar, and Code Spell Checker defaults for `.pib` files ([tooling design](tooling.md#vs-code-extension)).
- CI: builds both core targets, runs the tests on `net10.0` and `net8.0`, checks the sample story, and builds the VS Code extension.

**Exit:** every file in `samples/kitchen` parses, the completeness gates pass for syntax (every node kind snapshotted, every syntax diagnostic has a fixture, documentation examples extracted and parsed), the round-trip, totality and line-independence properties and mutation fuzzing pass, CI runs all of it on every push, and writers get highlighting in VS Code. Until the binder exists, a `pib` example passes if it parses with no syntax diagnostics, and a `pib-error` example is checked against its PIB1xxx codes only.

### Phase 2: Semantics

- Prelude, declaration pass, binder, type checking, flow and content checks, "did you mean" suggestions.
- `pibbles check` runs the full analysis. `pibbles ids`.
- The VS Code extension's line ID handling: **End**, arrow keys and clicks skip a line's trailing `#id:` ([tooling design](tooling.md#vs-code-extension)).

**Exit:** a fixture for every semantic diagnostic, and the sample story checks cleanly. **Writers can start real content here**, since the analyzer catches their mistakes even before the story can run.

### Phase 3: Runtime

- Compiler to IR, `DialogueRunner`, `StoryState` with versioned snapshots, runner snapshots, template rendering, `LineReveal`, host functions.
- `pibbles play`, both interactive and scripted. Benchmarks.

**Exit:** transcript tests cover every statement and inline element, the determinism, save-anywhere and skip-equivalence properties pass, the kitchen sample plays through all its branches in the terminal, and the benchmarks confirm the estimates in the [runtime design](runtime.md#performance) or the design is revisited.

### Phase 4: Godot integration (MVP)

- The adapter: loading, runner node, command and markup registries with launch checks, the export plugin, [failure handling](godot.md#failures), the BBCode renderer, the reveal label, icon provider hooks, save support.
- Run the culture-data probe on an exported desktop build ([architecture](architecture.md#target-frameworks)).
- A dialogue UI in a Godot project, built on the adapter, that hands control to gameplay and back.

**Exit:** the kitchen sample runs inside a Godot project with pacing, effects, icons and choices, the story can hand control to gameplay and back, and a save made mid-dialogue loads back into the same line. **This is the MVP.**

### Phase 5: Localization

- The PO reader and writer, `pibbles loc update`, translation validation (PIB4xxx), runtime locale lookup and fallback, number formatting, `required` markup and `///` notes.

**Exit:** a pseudo-locale (generated with accents and 30% longer text) runs in a Godot project, and every validation rule has a fixture.

### Phase 6: Editor tooling

- The language server with features 1–6 from the [tooling design](tooling.md#language-server) and [spell checking](tooling.md#spell-checking), and the VS Code client, which also hides line IDs except on the line under the cursor.

**Exit:** writers get live diagnostics, completion and go-to-definition in VS Code.

The order of phases 5 and 6 can swap, depending on whether translation or writer tooling becomes pressing first.
