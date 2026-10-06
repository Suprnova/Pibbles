# Architecture

How the pieces of Pibbles fit together, how the solution is laid out, which frameworks it targets, and how everything is tested. Each subsystem's design document covers its own part in detail ([AGENTS.md](../AGENTS.md#docs) lists them).

## Pipeline

```
 .pib sources ──► Line classifier ──► Parser ──► Syntax trees ──► Binder ──► Bound story ──► Compiler ──► Story
 (path, text)     (per line, INDENT/  (statement  (per file,      (symbols,   (types, refs,               (IR + line
                  DEDENT, kind)       + inline)   spans, errors)  types)      diagnostics)                templates)
                                                                                                             │
       Host ◄── Steps (Line, Choice, Command, Pose, Wait, End) ◄── DialogueRunner ◄── StoryState ◄───────────┘
        │                                                                 ▲
        └── LineReveal (optional): timing and markers for one Line        └── Host functions (delegates)
```

Each stage is a pure function of its inputs, except the runner, which mutates the `StoryState`. Diagnostics build up through the stages. A stage keeps going after errors and marks the broken pieces instead of stopping, so one run reports every problem.

## Solution layout

```
Pibbles.slnx
Directory.Build.props          C# 14, nullable, warnings as errors, code style enforced in builds
global.json                    SDK version; `dotnet test` runs on Microsoft Testing Platform
src/
  Pibbles/                     the core: net8.0 + net10.0, BCL only, no package references, trimming/AOT analyzers on
    Syntax/                    line classifier, parser, inline parser, syntax tree
    Semantics/                 symbols, types, binder, flow checks
    Diagnostics/               Diagnostic, severity, the diagnostic catalog
    Compilation/               IR, compiler, Story
    Runtime/                   DialogueRunner, StoryState, values, steps, host functions
    Text/                      Line, spans, markers, template rendering, LineReveal
    Localization/              string tables, PO reading and writing, translation validation (with localization)
  Pibbles.Cli/                 the `pibbles` .NET tool
  Pibbles.LanguageServer/      LSP server (Phase 6)
  Pibbles.Godot/               generic Godot adapter (Phase 4)
tests/
  Pibbles.Tests/
  Pibbles.Benchmarks/          BenchmarkDotNet (Phase 3)
samples/kitchen/               the sample story. It must always check cleanly, and its transcripts are tests.
editors/vscode/                TextMate grammar, snippets, line ID handling, LSP client (Phases 1, 2, 6)
docs/                          design documents, the language reference and the writer's guide
```

**One core assembly.** Splitting it into compiler and runtime assemblies would only pay off once a precompiled format exists, so a shipped game could leave the compiler out. Until then, the namespaces keep things organized, and a split can happen later without breaking anything.

**Sources are `(path, text)` pairs,** never file paths the core opens itself. Godot reads `res://` through its own `FileAccess` (inside an exported PCK, `System.IO` can't see the files), the language server works on unsaved editor buffers, and tests use in-memory strings. Only the CLI touches the disk.

## Target frameworks

- **Godot's C# packages set a minimum .NET version, not a fixed one.** Since Godot 4.4 that minimum is .NET 8, and projects are free to target newer versions ([Godot blog](https://godotengine.org/article/godotsharp-packages-net8/)). A game can target `net10.0`.
- **The core and `Pibbles.Godot` target `net8.0` and `net10.0`,** so any Godot 4.4+ project can use them. C# 14 compiles for both targets. The exceptions are features and APIs that need a newer runtime, which the core avoids or puts behind a `net10.0`-only code path. Every build compiles both targets, so an API or language feature .NET 8 lacks fails the build right away. Local test runs use `net10.0` only. CI also runs the tests on `net8.0`, through `-p:TestAllTargets=true`, which catches behavior that differs between the runtimes, such as Unicode segmentation and culture data.
- **The CLI, language server, tests and benchmarks** are tools, not dependencies, so they target `net10.0` only.

**Culture data:** some .NET deployments run in invariant-globalization mode, with no locale data. Number formatting must never assume the data is there. If a culture can't be loaded, Pibbles formats with the invariant culture and reports it once as a warning instead of throwing. Phase 4 checks an exported desktop build of a Godot project with a one-line probe: log `CultureInfo.GetCultureInfo("de-DE").NumberFormat.NumberDecimalSeparator`. It should print `,`. If it prints `.` or throws, the build is running without culture data.

## Testing strategy

Tests use xUnit v3 on Microsoft Testing Platform. Each test follows the Arrange-Act-Assert pattern and is named `Unit_Scenario_ExpectedBehavior`, with a constructor setting up a known state, and `[Theory]` wherever a test takes arbitrary data. The core is engine-free, so every feature is tested headlessly. Testing happens in four layers. Each one catches mistakes the others miss.

### 1. Example tests

These pin down *what* the language does, one construct at a time.

| Kind | What | How |
| --- | --- | --- |
| Syntax snapshots | Source → syntax tree dump | Snapshot files (Verify) under `tests/…/Snapshots` |
| Diagnostic fixtures | One `.pib` file per code, named after it, with each expected diagnostic marked on the line below: `// ^^^ PIB2003`, with the carets under the marked text, or `// PIB2003` for one that starts in the first two columns, where the `//` sits. A zero-length span gets one caret. Lines below a `// Must not trigger` comment are near misses. The file must produce exactly the marked diagnostics, so a near miss that triggers, or a second diagnostic cascading from the first, fails it. Fixtures under `Fixtures/Syntax` are only parsed, since they use names freely. Every other fixture compiles as a story of one file, and declares every name it uses. Fixtures leave out line IDs, like documentation examples, so a missing ID (PIB3010) is only checked in its own fixture. | One generic test runs every fixture file |
| IR snapshots | Source → IR listing | Snapshot files |
| Transcript tests | Script plus scripted choices → transcript of every step, marker and state change | `.pib` + `.transcript` pairs, the same format `pibbles play --script` prints |
| Reveal tests | A line and a sequence of `Advance`/`Skip` calls → frames | Plain unit tests |
| Sample story | `samples/kitchen` checks cleanly and its transcripts match | Integration test |

Fixture and transcript files double as executable documentation of the language. That makes them the main way agents and people confirm a feature works ([AGENTS.md](../AGENTS.md)).

### 2. Completeness gates

Example tests only prove the cases someone thought to write. These tests fail when a case is *missing*, which is how "the entire grammar is tested" becomes a checked fact instead of a hope:

- **Every syntax node kind** appears in at least one syntax snapshot.
- **Every diagnostic code** in `DiagnosticCatalog` has at least one fixture that triggers it.
- **Every step kind and marker kind** appears in at least one transcript.
- **Every code block in the language reference and the writer's guide** is extracted and checked, so the docs can't drift from the implementation, and every documented example is also a test. A block's info string says how it's checked:

  | Tag | Checked as |
  | --- | --- |
  | `pib` | Compiled together with the documentation prelude, `docs/language/examples.pib`, which declares every actor, variable, command, tag and node the examples use without declaring them. A block with no node header and no declaration is a fragment, and is wrapped in a node first. It must produce no errors and no warnings, except missing line IDs (PIB3010), since examples leave IDs out. |
  | `pib-standalone` | The same, but compiled without the prelude, for a block that declares its own cast, such as a sample cast list. |
  | `pib-error PIB2003 …` | Wrapped and compiled like `pib`, and must produce exactly the listed codes, in order. Missing line IDs aren't counted, since examples leave them out. |
  | Anything else (`text`, `json`, `ini`) | Not checked: folder trees, settings files, PO entries. |

  An untagged block fails the gate, and so does any other info string starting with `pib`, so nothing is skipped by accident or by a typo. When an example needs a new name, the prelude declares it in the same change.

Adding a node kind, diagnostic or step without a test then fails CI. That's the check agents need most, because they tend to consider a feature finished once it works.

### 3. Property-based tests

Some guarantees are too combinatorial for examples. These run as ordinary tests with a property-testing library (CsCheck, which is C#-first and shrinks failing cases to minimal ones). Every run, local or in CI, checks the same fixed sequence of cases, so the suite is deterministic. When a case fails, CsCheck shrinks it and the failure names its seed.

| Property | Guards |
| --- | --- |
| **Round-trip:** generate a random valid syntax tree, print it to source, parse it back, get the same tree with no diagnostics | Grammar coverage, operator precedence, escaping (the printer escapes `\`, `[`, `{` and `#` in text, `@` in option text, and `:` and a leading `-`, `/`, `=` or `@` in narration) |
| **Totality:** for any input, parsing and binding finish, never throw, and produce spans that lie inside the file and nest properly: each node's children lie inside it, in order | Error tolerance, which the language server depends on, since it sees half-typed text on every keystroke |
| **Line independence:** replacing one line of a valid file with garbage at the same indentation doesn't change the parse of any other line: the diagnostics that start on it, and the nodes that start on it, with their kinds, columns, values and the kinds of the nodes above them. The replaced line has no deeper-indented line under it, and the garbage doesn't start with `==`, `->`, `- `, `//`, `@elif` or `@else`. Replacing a block's opener, or writing a header, option, alternative, comment or clause, changes structure by design, and is covered by the totality property instead. | The line-oriented principle ([language design](language/design.md#design-principles)) |
| **Determinism:** the same story, choices and function stubs always give the same transcript | Variation selection, conditions |
| **Save anywhere:** play a random path, request a save at a random step, restore, and finish. The transcript matches an uninterrupted run, except for the replayed line. | Mid-dialogue saves and fast-forwarding, the riskiest runtime feature |
| **Skip equivalence:** skipping a reveal at any point fires the same effect markers, in the same order, as revealing it fully | Reveal skip semantics |

The round-trip property needs a syntax tree printer. That's a small piece of test code, and it could later become the basis of the formatter. A test checks that the tree generator produces every node kind except the error node, so no part of the grammar drops out of the round-trip unnoticed.

**Longer and random runs.** Setting one of CsCheck's environment variables switches the properties from the fixed sequence to CsCheck's own random search:

```text
CsCheck_Iter=100000 dotnet test -- --filter-namespace Pibbles.Tests.Properties    (more cases, from random seeds)
CsCheck_Time=300 dotnet test -- --filter-namespace Pibbles.Tests.Properties       (five minutes per property)
CsCheck_Seed=<seed> dotnet test -- --filter-method "*<test name>"                 (replay and shrink a reported failure)
```

A bug found this way gets a fixture or a recovery snapshot when it's fixed, so the fixed sequence doesn't need to find it again.

### 4. Mutation fuzzing

The totality property also runs on *mutated real input*: `samples/kitchen`, the starter story, every fixture and snapshot input, and every documentation example, with random characters and lines deleted, duplicated, swapped or replaced with Pibbles punctuation (`@ -> == [ ] { } $ # \ :` and indentation). This finds crashes in near-miss input, the kind writers actually produce, much faster than purely random strings. It runs as part of the normal test suite, with a fixed budget.

**Coverage-guided fuzzing** (SharpFuzz with libFuzzer) is a stretch goal. Scripts are trusted content, not attacker input, so the security case for it is weak. It's worth an occasional long run only if the property and mutation tests keep turning up crashes.

### What isn't tested here

- **Rendering and timing inside Godot.** The adapter is kept thin, and it's checked by running the sample in a Godot project ([roadmap](roadmap.md)).
- **Games' real stories.** They aren't part of this repository. A game's own CI runs `pibbles check` and its transcripts against its story, which makes real games the largest tests Pibbles gets.
- **Benchmarks** (`Pibbles.Benchmarks`) measure; they don't pass or fail. They run on demand.

## With extensions

What later tiers ([roadmap](roadmap.md#feature-tiers)) add to the testing strategy.

- **Localization:** fixtures that pair a source and a translation with the validation diagnostics they produce, and the **translation identity** property: a line validates against itself, moving presentation elements keeps it valid, and dropping any effect makes it invalid.
- **Release manifests:** the save-across-releases property ([runtime design](runtime.md#release-manifests-and-migrations)).
