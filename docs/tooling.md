# Tooling design

Tooling is how the "fail at edit time" principle reaches writers. There are three surfaces, all built on the same core: a CLI, a VS Code extension, and the Godot adapter's export and launch checks.

## CLI (`pibbles`)

Packaged as a .NET tool (`dotnet tool install Pibbles.Cli`, which installs the `pibbles` command; NuGet package IDs ignore case, so the tool can't share the core library's `Pibbles`), and also runnable from the repo with `dotnet run --project src/Pibbles.Cli`. `[root]` is the [project root](language/reference.md#files-and-structure), the current directory by default.

| Command | Purpose | Phase |
| --- | --- | --- |
| `pibbles check [root]` | Compiles the story and prints diagnostics as `file(line,col): severity CODE: message`, the MSBuild format that editors and CI already understand. Exits non-zero on errors, or on warnings with `--warnaserror`. `--release` checks what a release build would ship; in v1 that's everything, and [drafts](#with-extensions) give it meaning later, so CI's command never has to change. CI runs `--release --warnaserror`. `--style` also shows hints ([semantics design](semantics.md)). `--format json` gives tools machine-readable output. Severities come from `.editorconfig`. | 1 (syntax), 2 (full analysis and `.editorconfig`) |
| `pibbles play [root] --start <node>` | Plays the story in the terminal: lines with speaker and pose, markers shown inline (`⟨w 0.5⟩`, `⟨@sfx thud⟩`), numbered choices. `--set $var=value` seeds variables. Host functions are stubbed through `--stub has_item=true` or a stub file. | 3 |
| `pibbles play … --script <file>` | Non-interactive: takes choices from a file and prints a deterministic transcript. It's the same format the transcript tests use, so a writer's reproduction of a bug becomes a test by copying files. | 3 |
| `pibbles ids [root]` | Adds missing `#id:` tags in place | 2 |
| `pibbles loc update [root]` | Regenerates `template.pot` and merges it into every `<locale>.po` | 5 |
| `pibbles voice script` | Exports a recording script per actor, one row per wording variant | Stretch |
| `pibbles voice accept <id>` | Marks a recording as still matching its line after an edit | Stretch |
| `pibbles graph [root]` | Writes the node flow graph as DOT or Mermaid, for reviewing branching | Stretch |
| `pibbles lsp` | Runs the language server over stdio (see below) | 6 |

Every diagnostic code is listed in the [diagnostics catalog](diagnostics.md).

`pibbles play` matters more than it looks. Writers can test a branch without launching the game or knowing C#, and it gives coding agents an end-to-end check that needs no engine.

## VS Code extension

It lives in `editors/vscode/`. CI builds it as a `.vsix`, which writers install locally and which is attached to GitHub pre-releases. It isn't published to the VS Code Marketplace. It ships in two steps:

1. **Syntax highlighting (right after Phase 1),** as a TextMate grammar, which needs no server. It highlights headers, speakers and poses, `@` statements, options, inline `[markup]` and `{points}`, tags and comments. Snippets cover common shapes (`@if`, choices, variation blocks). `#id:` tags are faded, or hidden except on the line under the cursor ([localization design](localization.md#keeping-them-out-of-the-way)). This is cheap and gives writers most of the day-to-day benefit.
2. **Language client (Phase 6),** which starts `pibbles lsp`.

Until the language server exists, the extension contributes default settings for the [Code Spell Checker](https://marketplace.visualstudio.com/items?itemName=streetsidesoftware.code-spell-checker) extension, if the writer has it installed: it enables checking for `.pib` files, skips `@` lines, speakers and poses, tags, comments, `[markup]` and `{points}` with regular expressions, and reads `words.txt` as a custom dictionary. The patterns only approximate the grammar and know nothing about actors, so this is a stopgap. The language server's [spell checking](#spell-checking) replaces it, and the defaults are removed once it ships. Words added in the meantime carry over, since both read the same `words.txt`.

Writers will use VS Code, not Godot's script editor, for `.pib` files. Godot's editor has no extension point for custom language servers. The Godot adapter covers the in-engine side by printing diagnostics to the output panel on load.

## Language server

The language server is a thin layer over the `SemanticModel` ([semantics design](semantics.md#passes)). On every change it reparses the edited file and re-binds the project. Features, in priority order:

1. **Diagnostics,** published per file.
2. **Completion:** actors after line start, poses inside `(`, commands after `@`, parameters and enum members in arguments, node names after `@jump`/`@call` (in relative form for nodes under the file's prefix), variables after `$`, markup after `[`, icons after `{icon `, tags after `#`.
3. **Go to definition:** jump targets, variables, commands, actors, markup.
4. **Hover:** declaration signatures, with the declaration's `///` notes as documentation, and diagnostic explanations from the catalog.
5. **Find references and rename:** for every declared name. Rename stops at a line boundary and never rewrites dialogue. Renamed nodes always get a `#was:` alias, and so do renamed variables once [release manifests](#with-extensions) arrive (the `pibbles.rename.aliases` setting turns this off). Renaming a file's `@prefix` renames each of its relative-named nodes, so each gets an alias. For names the game's code uses, the preview warns that the code must change too ([semantics design](semantics.md#pib5031--naming-convention)).
6. **Code actions:** extract selected statements to a new node ([semantics design](semantics.md#extract-to-node)), add missing line IDs (also optionally on save), generate a migration header covering every retired ID that has nowhere to go (using node and range claims where possible), give a pasted duplicate a fresh ID, escape a colon that looks like a speaker, apply "did you mean" fixes, add an unknown word to `words.txt` ([spell checking](#spell-checking)).
7. **Semantic tokens,** to refine TextMate highlighting where the grammar can't tell things apart, such as a known versus unknown actor.
8. **Stretch:** document outline (nodes), folding, and a hover preview that renders a line's resolved text with its markers.

**LSP library:** we'll evaluate this in Phase 6. The options are `OmniSharp.Extensions.LanguageServer`, or `StreamJsonRpc` with a hand-written subset of protocol types. Only diagnostics, completion, definition, hover, references, rename, code actions and semantic tokens are needed, and that subset is small enough that a hand-written protocol layer is realistic if the library turns out heavy or unmaintained. The server lives in its own project, so the core never picks up the dependency.

## Spell checking

The language server spell-checks the text players see. It lives there rather than in a generic spellchecker because the analyzer already knows which parts of a line are text and which are syntax, and it knows every actor's name.

- **What's checked:** the text runs of every line and option, including every `{if}` branch and variation alternative, and term bodies. This is the same text that goes to translators.
- **What isn't:** speakers, poses, commands and their arguments, markup names, points, tags, comments, `///` notes, and string literals in expressions (`has_item("crowbar")`).
- **Word boundaries:** markup doesn't split a word, so `[b]un[/b]believable` is checked as one word. Points do, so in `walk{s}` only `walk` is checked.
- **Deliberate misspellings** are common in character voice. Words with a letter repeated three or more times (`Spooooky`) and words cut off with a dash (`Wait for m—`) are skipped. Anything else is added to `words.txt`, or suppressed for one line with `// pibbles-ignore PIB6001`.
- **Severity:** unknown words are PIB6001, an info diagnostic, so spelling never fails `--warnaserror`. `.editorconfig` can change it like any other diagnostic, through the `spelling` category.
- **Language server only.** The CLI doesn't check spelling, and the core never sees a dictionary, so it stays BCL-only and games ship without one.

### Dictionaries

A word is accepted if any of these contain it:

1. **The source locale's dictionary.** A Hunspell dictionary for `sourceLocale` in `pibbles.json` (`en` without one). The server bundles `en`.
2. **`words.txt` in the story folder.** A committed plain-text file with one word per line, optional. The "Add to dictionary" code action creates it or appends to it, keeping it sorted so parallel additions merge cleanly.
3. **Declared names.** Actor display names and persona names (`name: Mira`) are added automatically. A name is rarely misspelled where it's declared, so this also makes "did you mean" suggest `Mira` first for `Mirra`, using the same edit-distance suggestions as other diagnostics.

Hunspell dictionaries are read with [WeCantSpell.Hunspell](https://github.com/aarondandy/WeCantSpell.Hunspell), a pure .NET port. Its license, and the bundled `en` dictionary's, are confirmed in Phase 6 before depending on them.

Translations aren't checked yet. The same check can later run on each `<locale>.po` with that locale's dictionary and its own word list.

## Configuration

Diagnostics are configured in `.editorconfig`, the way Roslyn analyzers are in .NET. Settings go in a `[*.pib]` section. They can live in the repository's existing `.editorconfig`, or in one under `story/`, and nested files override them per folder:

```ini
[*.pib]
indent_style = space
indent_size = 4

# Any diagnostic: error, warning, info, hint or none
pibbles_diagnostic.PIB5003.severity = none
pibbles_diagnostic.PIB5040.severity = hint

# A whole category at once: syntax, binding, content, localization, style or spelling
pibbles_diagnostic.category-style.severity = none

# Thresholds
pibbles_max_nesting = 3
pibbles_max_option_body = 15
pibbles_min_repeated_lines = 3
pibbles_min_repeated_colors = 2
pibbles_min_repeated_conditions = 2
pibbles_max_message_length = 300
pibbles_max_option_length = 80

[story/drafts/**.pib]
pibbles_diagnostic.category-style.severity = none
```

Each threshold belongs to one of the [style rules](semantics.md#style-rules). Drafts don't need this for IDs, localization or voice, since those exemptions come with [draft status](language/design.md#drafts). The section above only silences style hints while a draft is rough.

- **Standard keys are respected.** `indent_style` and `indent_size` are what PIB1001 and PIB5032 check against. Without them, PIB5032 only checks that a file is consistent with itself.
- **A specific code beats its category,** and later, more specific sections beat earlier ones, following the usual `.editorconfig` rules.
- **What stays in `pibbles.json`:** project facts that aren't per file, such as the version, source locale, localization and voice. `pibbles.json` describes what the project *is*. `.editorconfig` describes how strictly each file is checked.
- **The core stays free of I/O.** It parses `.editorconfig` text and resolves settings for a given path. The CLI, language server and adapter find the files and pass their contents in, the same way sources are passed ([architecture](architecture.md#solution-layout)).

## With extensions

What the [grammar extensions](language/design.md#grammar-extensions) and later tiers ([roadmap](roadmap.md#feature-tiers)) add to the tooling.

**CLI:**

| Addition | Arrives with |
| --- | --- |
| `pibbles check --release` compiles without drafts, so a broken draft never blocks CI | Drafts |
| `pibbles check` also compiles every locale | Localization |
| `pibbles ids` skips drafts | Drafts |
| `pibbles play --seed <n>` fixes the random generator. Without it, a random seed is used and printed, so the run can be repeated. | `random()` and `@shuffle` |
| `pibbles play --locale` picks a language | Localization |
| `pibbles release <version>` records what a shipped build contained (`story/releases/<version>.json`), so `check` can guarantee every old save still resolves | Release manifests |
