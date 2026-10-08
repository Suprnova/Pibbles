# Pibbles

Pibbles is a narrative scripting language for visual novels, plus a .NET library that compiles and runs it. The core is engine-agnostic, and Godot is the first supported engine.

## Map

```text
Pibbles.slnx
Directory.Build.props        shared build settings: C# 14, nullable, warnings as errors
global.json                  SDK version; `dotnet test` runs on Microsoft Testing Platform
src/Pibbles/                 the core: net8.0 + net10.0, BCL only
src/Pibbles.Cli/             the `pibbles` .NET tool
tests/Pibbles.Tests/         xUnit v3 tests for the core, the CLI and the docs
tests/Pibbles.Benchmarks/    BenchmarkDotNet benchmarks over a generated story; built, never run by `dotnet test`
samples/kitchen/             the sample story
editors/vscode/              the VS Code extension: the TextMate grammar (YAML), the editing aids and their tests, built with Node
docs/                        the living documentation: design docs, the language reference and guide
```

[docs/architecture.md](docs/architecture.md#solution-layout) has the full layout, including the projects later phases add.

## Commands

```text
dotnet build
dotnet test
dotnet test -p:TestAllTargets=true                                                             (net8.0 too, as CI does)
dotnet run --project src/Pibbles.Cli -- check samples/kitchen
dotnet run --project src/Pibbles.Cli -- ids samples/kitchen                                    (after adding lines to the sample)
npm test --prefix editors/vscode                                                                (the extension and grammar; npm ci there first)
dotnet run --project src/Pibbles.Cli -- test samples/kitchen                                   (replays the sample's transcripts)
dotnet run --project src/Pibbles.Cli -- play samples/kitchen --script <file>                   (prints a script's transcript; --start <node> without --script plays interactively)
dotnet run -c Release --project tests/Pibbles.Benchmarks -- --filter '*Compile*'               (benchmarks, on demand)
```

## Invariants (never break these)

- `src/Pibbles` references only the BCL. No engine types, no NuGet packages, no reflection-based binding; it must stay trim/AOT-safe.
- `src/Pibbles` builds for `net8.0`. Newer runtime APIs go behind `#if NET10_0_OR_GREATER`. Every build compiles both targets; CI runs the tests on both.
- The core never reads files; sources are `(path, text)` pairs.
- The parser never throws on user input; every problem is a `Diagnostic` with a catalogued code.
- Every diagnostic code is registered in `DiagnosticCatalog` and documented in `docs/diagnostics.md`.
- `docs/language/reference.md` is authoritative for syntax and semantics. If code and reference disagree, the change is not done.
- `samples/kitchen` checks cleanly and its transcripts pass at all times.
- Committed files never name or depend on a specific consuming project. Samples and fixtures use original, neutral content.

## Workflows

These checklists are where work tends to stop too early. Finish every step.

**Adding or changing a language construct**

1. Update `docs/language/reference.md`, plus `docs/language/guide.md` if writers can see the change (and `docs/language/design.md` if the reasoning changes). A [grammar extension](docs/language/design.md#grammar-extensions) moves out of the design doc into both in the same change.
2. Line classifier and parser, plus syntax snapshot tests.
3. Binder and checks, plus diagnostic fixtures for every new error.
4. Compiler and runtime, plus transcript tests.
5. Translation validation category, if it's an inline element ([localization design](docs/localization.md#what-a-translation-may-change)).
6. TextMate grammar and, if relevant, language-server completion.
7. Use it in `samples/kitchen` if it's a user-facing feature.

**Adding a diagnostic**

1. Pick the next free code in the right range, and register it in `DiagnosticCatalog`.
2. Add a fixture that triggers it and one that almost does but must not.
3. Document it in `docs/diagnostics.md`.

**Adding a CLI command**

1. Document it in `docs/tooling.md`.
2. Test it through the command's handler, not by spawning a process.

## Testing

- Tests use xUnit v3 and follow the `testing-csharp-code` skill: Arrange-Act-Assert, `Unit_Scenario_ExpectedBehavior` names, and `[Theory]` for arbitrary data.
- The test layers, fixture and transcript formats, and completeness gates are in [docs/architecture.md](docs/architecture.md#testing-strategy).
- Every code block in `docs/language/reference.md` and `docs/language/guide.md` has an info string (`pib`, `pib-standalone`, `pib-error …`, or a non-Pibbles language like `text`). `pib` blocks compile against `docs/language/examples.pib`, so a new name in an example is declared there in the same change.

**The verification loop.** Confirm a change without Godot:

1. `dotnet test`: snapshots, fixtures, transcripts, reveal tests.
2. `pibbles check samples/kitchen` and `pibbles test samples/kitchen`: the sample story still compiles cleanly, and its transcripts still match.
3. `pibbles play … --script <file>`: the behavior a change describes, shown as a transcript. Save it under the sample's `transcripts/` and it becomes a test.

Only the Godot adapter's rendering and timing glue needs the engine, and it's kept small on purpose.

**Snapshot updates need care.** When accepting a changed snapshot, say in the summary why the new output is right. A snapshot that changes as a side effect of an unrelated change is a signal to investigate, not to accept.

## Docs

Read the relevant design doc before changing a subsystem, and update it in the same change when the design moves. A design doc says what a subsystem does and why. `docs/` holds only living documentation that stays true as the code changes. Implementation plans and other working notes aren't committed.

| Doc | Covers |
| --- | --- |
| [architecture.md](docs/architecture.md) | Pipeline, solution layout, target frameworks, testing strategy |
| [roadmap.md](docs/roadmap.md) | Feature tiers and phases with exit criteria |
| [boundaries.md](docs/boundaries.md) | What Pibbles does and what the host does |
| [diagnostics.md](docs/diagnostics.md) | Every diagnostic code |
| [language/reference.md](docs/language/reference.md) | The authoritative language specification, with the grammar |
| [language/guide.md](docs/language/guide.md) | The language for writers: what's possible, no implementation |
| [language/design.md](docs/language/design.md) | Goals and principles, and the designed grammar extensions |
| [syntax.md](docs/syntax.md) | Line classifier, parser, syntax tree, diagnostics infrastructure |
| [semantics.md](docs/semantics.md) | Binder, types, checks, style rules |
| [runtime.md](docs/runtime.md) | Compiler, runner, state, saves, text, reveal, performance |
| [localization.md](docs/localization.md) | Line IDs, translation workflow and validation, voice |
| [tooling.md](docs/tooling.md) | CLI, VS Code, language server, spell checking, `.editorconfig` |
| [godot.md](docs/godot.md) | The Godot adapter |

**Keep the docs current.** Docs describe the design as it is now, with v1 in the body and later features in a final "With extensions" section. When a doc turns out wrong, missing or out of date, fix it in the same change, or raise it if the fix is a design decision. If agents keep making the same mistake, the answer is a new invariant or checklist item here.
