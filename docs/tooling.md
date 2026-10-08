# Tooling design

Tooling is how the "fail at edit time" principle reaches writers. There are three surfaces, all built on the same core: a CLI, a VS Code extension, and the Godot adapter's export and launch checks.

## CLI (`pibbles`)

Packaged as a .NET tool (`dotnet tool install Pibbles.Cli`, which installs the `pibbles` command; NuGet package IDs ignore case, so the tool can't share the core library's `Pibbles`), and also runnable from the repo with `dotnet run --project src/Pibbles.Cli`. CI packs the tool on every build as a prerelease of the next version (`0.1.0-ci.<run>`) and uploads it as the `pibbles-cli-nupkg` artifact. Install a downloaded one with `dotnet tool install --global Pibbles.Cli --add-source <folder> --prerelease`, or `update` instead of `install` to replace an earlier one. A [release](#releases) attaches it at its plain version, which installs the same way, without `--prerelease`. `[root]` is the [project root](language/reference.md#files-and-structure), the current directory by default.

| Command | Purpose | Phase |
| --- | --- | --- |
| `pibbles init [folder]` | Starts a project in `folder` (the current directory by default, made if it doesn't exist): a `pibbles.json` with the current settings, and a story folder holding a short example story whose comments explain each part, with line IDs, which `pibbles check --warnaserror` passes. `--blank` leaves the story folder empty. It never overwrites an existing `pibbles.json`, and leaves a story folder that already has `.pib` files as it is. Writers rarely know JSON, so this is how a project's settings file gets made. | 1 |
| `pibbles check [root]` | Compiles the story and prints each diagnostic with its source line, the problem marked, and a fix ([how diagnostics read](syntax.md#how-diagnostics-read)). `--format msbuild` prints one line per diagnostic instead, as `file(line,col): severity CODE: message`, the format editors and CI annotations understand. `--format json` gives tools machine-readable output. Color is used only when writing to a terminal, and never when `NO_COLOR` is set. With color, the problem is highlighted in its source line as well as marked under it, quoted code in messages is cyan instead of in backticks, and the help and its fixed line are green; source lines are always shown exactly as written. Exits non-zero on errors, or on warnings with `--warnaserror`. `--release` checks what a release build would ship; in v1 that's everything, and [drafts](#with-extensions) give it meaning later, so CI's command never has to change. CI runs `--release --warnaserror --format msbuild`. `--style` also shows hints ([semantics design](semantics.md)). Severities come from `.editorconfig`. | 1 (syntax), 2 (full analysis and `.editorconfig`) |
| `pibbles explain <code>` | Prints a diagnostic's full entry: what it means, an example that triggers it, and how to fix it. The same text as the [diagnostics catalog](diagnostics.md). | 1 |
| `pibbles play [root] --start <node>` | Plays the story in the terminal, in the transcript's own layout, with color on a terminal. The writer answers choices by number or `#id`, and types the value of any host function call that nothing stubs. `--set`, `--stub` and `--stubs` seed variables and stub functions, and `--record <file>` writes the session as a transcript ([below](#playing-a-story)). | 3 |
| `pibbles play [root] --script <file>` | Plays a script and prints its transcript, exactly as the transcript tests compare it, so a writer's reproduction of a bug becomes a test by copying files ([below](#scripts-and-transcripts)). | 3 |
| `pibbles test [root]` | Replays every transcript under the project's `transcripts/` folder against the story, shows where each one that no longer matches first differs, and ends with a summary. `--update` rewrites the ones that don't match ([below](#testing-transcripts)). | 3 |
| `pibbles ids [root]` | Adds a line ID to every line that needs one and doesn't have it, in place ([below](#line-ids)) | 2 |
| `pibbles upgrade [root]` | Rewrites the project's `pibbles.json` in the newest schema, keeping its settings ([project settings](#project-settings)). It finds the project the way `check` does, and a file that's already current is left as it is. | 2 |
| `pibbles loc update [root]` | Adds missing line IDs as `pibbles ids` does, then regenerates `template.pot` and merges it into every `<locale>.po` | 5 |
| `pibbles voice script` | Exports a recording script per actor, one row per wording variant | Stretch |
| `pibbles voice accept <id>` | Marks a recording as still matching its line after an edit | Stretch |
| `pibbles graph [root]` | Writes the node flow graph as DOT or Mermaid, for reviewing branching | Stretch |
| `pibbles lsp` | Runs the language server over stdio (see below) | 6 |

Every diagnostic code is listed in the [diagnostics catalog](diagnostics.md).

**How `pibbles check` reads the story and reports it:**

- **The project** is found from the folder given, or the current directory, by going up to the nearest folder with a `pibbles.json` ([project root](language/reference.md#files-and-structure)). So `pibbles check` works from anywhere inside a project, such as its `story/` folder. `pibbles init` doesn't search: it starts a project exactly where it's told.
- **The story** is every `.pib` file under the story folders: `story/` under the root, or the folders that `pibbles.json`'s `storyFolders` setting lists. A file under two of them, because one folder is inside another, is read once.
- **Paths in diagnostics** are relative to the current directory, so they point at the right file from an editor's terminal or a CI job, whichever folder the root is.
- **The readable format** ends with a summary line, such as `Checked 3 files: 2 errors and 1 warning.` The `msbuild` and `json` formats print only the diagnostics.
- **Missing line IDs (PIB3010)** all share one fix, so when there are more than five, the readable format shows them as one entry after the other problems, with how many each file has and the fix: run `pibbles ids`. Five or fewer are shown one by one, and the summary line counts each one either way. The `msbuild` and `json` formats always list every one, since tools and CI annotations need each location.
- **JSON** is an array with one object per diagnostic: `path`, `line`, `column`, `endLine` and `endColumn` (1-based), `severity`, `code`, `message`, `label` and `help`.
- **Exit codes:** 0 when the check passes, 1 when an error fails it (or a warning, with `--warnaserror`), and 2 when there's nothing to check, such as a missing story folder. `pibbles explain` exits 2 for a code it doesn't know.
- **In CI,** the workflow registers `.github/pibbles-problem-matcher.json`, which turns `--format msbuild` output into annotations on the changed lines.

### Scripts and transcripts

A **script** says where to start and what to answer. A **transcript** is a script with every step written in, so any transcript is also a valid script: the reader only takes the directives and the answers, which are the lines that start in column 0, and ignores everything else. Copying a run into the tests is a file copy.

```text
start kitchen.door
set $has_key = true
stub has_item("crowbar") = false
stub has_item = false

  pose mira worried
  mira: Locked.⟨w⟩ Of course it's locked.  #k7qp2x
  choice
    1. Rattle the handle  #c4ne8r
    2. Use the key #show_disabled  #m3xw9a
    3. Knock politely  #t5bq2m
    4. Leave it  #x2jm5p
> #m3xw9a
  @sfx key_turn
  $door_open = true
  mira (happy): Got it!  #h8ya3k
```

- **Directives** are `start <node>` (required; a later `start` begins when the dialogue before it ends, with the state kept, which is how a script visits a node twice), `set $var = value` (seeds a variable before the first start), `stub fn(args) = value` (the value for one set of arguments) and `stub fn = value` (the value for any others). Host functions are side-effect free, so a stub is just a value. A function the story declares with no stub fails at the start, listing every one that's missing (the check is `HostFunctions.Validate`, so it covers every declared function, not only the ones a branch calls), and a call with arguments no stub matches fails naming the call.
- **Answers** are `> #id`, which picks an option by its ID (the writer always writes this), or a hand-written `> 2`, which picks the second option as written in the source, counting options `@once` has removed, so the same number means the same option on every visit. The writer uses `> N` for an option without an `#id`. A script that runs out of answers at a choice fails, naming the choice.
- **Steps are indented** by two spaces, and anything free-form in them has its backslashes and line breaks escaped (`\n`, `\\`). Directives and answers are at column 0, so no step can be read as one, whatever its text says.
- **What's printed:** every step; every marker inline at its position (`⟨w⟩`, `⟨w 0.5s⟩`, `⟨p⟩`, `⟨speed 0.3⟩`, `⟨speed⟩`, `⟨@sfx thud⟩`, with ` wait` when the command waits), icons inline (`⟨icon interact⟩`), spans as in the source with all their arguments (`[wave 2 5]…[/wave]`), each option of a choice with its source number, its ID and its state (`(unavailable)`, `(chosen)`), each command's `waits`, every variable change (`$var = value`) before the step that followed it, every runtime warning with its kind and location, and a line's tags and ID after its text. A fallback ID is written `#~`, since it changes whenever lines move. Numbers are normalized, strings quoted, durations written with `s`.
- **There is no version header.** The format only ever grows.

`pibbles play` matters more than it looks. Writers can test a branch without launching the game or knowing C#, and it gives coding agents an end-to-end check that needs no engine.

### Playing a story

`pibbles play` finds and compiles the project the way `check` does. A story with errors doesn't play: the errors print as `check` prints them, on stderr, and play exits 1. Warnings don't stop it, since a line written a minute ago can be playtested before it has an ID; stderr gets one line, such as `2 warnings; run pibbles check to see them.`, and stdout stays a clean transcript.

- **With `--script <file>`, play is scripted.** It plays the script and prints the transcript to stdout, byte for byte what the transcript tests compare. `--start` is only needed when the script has no `start`.
- **Without `--script`, play is interactive,** and `--start <node>` says where. Steps print as a transcript prints them: what a writer sees is what a transcript records. On a terminal, and unless `NO_COLOR` is set, directives are blue, answers green, speakers bold, markers cyan, stage steps (commands, poses, waits, variable changes, the `choice` header and `end`) magenta, warnings yellow, and IDs dim. Redirected, the output is plain.
  - **Choices** list each option with its source number and state, as a transcript does. The writer answers with a number or an `#id`; an answer that isn't on offer or isn't available says why and asks again, and the answer is printed as `> #id`.
  - **Pacing:** play waits for Enter after each line, so a scene reads at the pace it would in the game. `--no-pause` prints straight through to the next choice instead.
  - **Missing stubs are asked for.** The first time the story calls a function with arguments no stub covers, play asks what it returns, checks the answer against the function's return type (asking again if it doesn't fit), and remembers it for the rest of the session. A story with host functions plays without any stubs at all.
  - **Ending:** the dialogue's end exits 0. End of input (Ctrl+D, or Ctrl+Z then Enter on Windows) stops play with a short message on stderr and exits 0, since it's how a writer leaves a session. Ctrl+C stops it with a short message too, and no stack trace, and the process exits with the system's interrupt code (130 on Linux and macOS). Either way, what was recorded so far is still written.
- **`--record <file>`** writes the session as a transcript, so an interactive bug report becomes a test. Its directives are the `start`, the `set` and `stub` directives in effect, a `stub` line for each value typed at a prompt (`stub price("rope") = 6`, or `stub lucky = true` for a function without parameters), and a `stub` with the plainest value of its return type (`false`, `0`, `""`, `0s`, or the first member, actor or node) for each function the session never called, because a script must stub every declared function. Then come the steps and the answers. Replaying it with `--script` prints the same file. It works in scripted play too.
- **Flags add directives,** with the same syntax as a script's: `--set '$var=value'` and `--stub 'fn=value'` or `--stub 'fn(args)=value'`, each repeatable, and `--stubs <file>`, a file of `stub` lines and blank lines only. They come after the script's own directives, the stubs file's lines before `--stub`, and a later directive wins, so a flag overrides the script without editing it. The transcript lists them after the script's own, so it replays to the same run. `--start` replaces the script's first `start`, or comes first if it has none.
- **Failures are the script's:** a declared function with no stub (all listed at the start), a call no stub matches, a choice with no answer left, or a directive that can't be read. Each prints a message to stderr and exits 1. Interactive play has no such failures for stubs and answers, since it asks.
- **Exit codes,** as `check`'s: 0 when the script or the session played to its end, or the writer ended it with end of input; 1 for a story with errors or a failing script; 2 when there's nothing to play: no story folder, no `--start` or `--script`, or a script or stubs file that can't be read.

### Testing transcripts

`pibbles test [root]` checks that a project's transcripts still match its story. A game's CI runs it next to `pibbles check`, and so does this repository's, for the sample story.

- **It runs every `.transcript` file** under the `transcripts/` folder beside `pibbles.json`, at any depth, against the compiled story, as a script, and compares what it prints with the file. A file matches when the two are the same once the file's line endings are `\n`, the same rule the repository's own transcript tests use.
- **A transcript that doesn't match** prints `FAILED`, its path and the line where it first differs, then up to five lines of what the file says (`-`) and of what the story prints now (`+`) from that line on. One whose script fails, such as a node that no longer exists, prints `FAILED`, its path and why.
- **It ends with a summary line,** such as `Ran 13 transcripts: 12 passed, 1 failed.`
- **`--update`** rewrites each transcript that doesn't match with what the story prints now, keeping the file's line endings, and prints `UPDATED` and its path. It never touches a transcript that matches, and can't fix one whose script fails. Use it once you've decided the change is right, as with any snapshot.
- **Exit codes:** 0 when every transcript matches or was updated, 1 when one doesn't or the story has errors, and 2 when there's nothing to run: no story, or no `transcripts/` folder or none in it.

### Line IDs

`pibbles ids` gives every line that needs a [line ID](localization.md#line-ids) and doesn't have one a new one: text lines that show text, options, `@call` and variation block openers. It finds the project the way `check` does.

- **An ID goes at the end of its line,** after the line's tags, and before a trailing `//` comment on an `@` line: `@call kitchen.stuck #id:r4hc6v // Back down.` Nothing else in the file changes, including its line endings and any byte-order mark.
- **A new ID is a lowercase letter and five lowercase letters or digits,** and never matches an ID, node name or old node name already in the story.
- **A file with a PIB1xxx mistake,** the kind found while reading its lines, is left alone and listed, since a misread line could get its ID in the wrong place. The other files still get their IDs, and the command exits 1. Mistakes found later, such as an unknown actor, don't stop it.
- It prints `Added 27 IDs in 1 file.`, or `Every line already has an ID.`, so running it twice changes nothing the second time.
- **`pibbles init`** gives its example story IDs the same way, so a new project passes `pibbles check --warnaserror`.

The core does the work in `LineIds.AddMissing`, which returns the text to insert and touches no files, so the language server can offer the same as a code action.

## VS Code extension

It lives in `editors/vscode/`. CI builds it as a `.vsix`, which writers install locally and which is attached to every [release](#releases). It isn't published to the VS Code Marketplace. It ships in three steps:

1. **Syntax highlighting (Phase 1),** as a TextMate grammar, which needs no server and no extension code. It highlights headers, speakers and poses, `@` statements, options, inline `[markup]` and `{points}`, tags and comments. Snippets cover common shapes (`@if`, choices, variation blocks). `#id:` tags get a comment scope, so every theme shows them faded. Enter indents under a block opener, option or bare `-`, and Enter after an indented `@end`, `@jump` or `@return` moves out one level, since nothing after them in their block can run. Enter on a line that holds only indentation also moves out one level, since a block has no closing line, and Enter on an empty line stays at column 0 rather than returning to the last indented line's level: one Enter after an alternative stays ready for the next `- `, and a second leaves the block. This is cheap and gives writers most of the day-to-day benefit.
2. **Editing aids (Phase 2),** as a small TypeScript extension, when `pibbles ids` starts writing line IDs:
   - **Line IDs.** In `.pib` files, a line's trailing `#id:` is hidden, and the status bar shows the ID of the cursor's line instead. Clicking it, or a key binding (**Ctrl+Alt+I**, or **Cmd+Alt+I** on a Mac), edits the ID in a box that accepts only an ID's shape, for the rare edit. VS Code has no supported way to hide text in a line, so a decoration collapses the ID's text, and the cursor never goes where the hidden text is: a cursor that would land in it or after it goes back to the content's end, and a selection end that would land in it goes back too, though a selection can still take whole lines. A selection of nothing but hidden text, as a double-click past the end of a line makes, becomes a cursor at the content's end, so an ID can't be selected or dragged. Only the one space before an ID is hidden with it, so spaces typed at the end of a line show as text. An ID typed by hand stays shown, with the cursor free to move in it, until the cursor leaves its line. The cursor works the same way with IDs shown: **End**, **Shift+End** and **Right** treat the ID as if it weren't there, and a click past the end of a line puts the cursor before it, so typing at the end of a line never lands after its ID. Enter just before a trailing ID breaks the line after it, so the ID stays with its line. **Backspace** and **Delete** that join lines keep each ID with its line too: a line deleted one character at a time, or selected from the end of the line above, takes its ID with it, and the line above keeps its own at the end. Delete at the content's end joins the next line, as Right goes to it. A deletion that starts or ends inside an ID is left alone, as a deliberate edit of that ID. On a Mac, **Cmd+Right** and **Shift+Cmd+Right** work like End. The `pibbles.lineIds.hide` setting shows IDs again, faded. With IDs shown, a click directly on an ID puts the cursor in it, pressing End again goes to the line's real end, and the `pibbles.lineIds.skipWithCursor` setting turns the cursor's skipping off.
   - **Alternatives,** like a Markdown list. Enter at the end of an alternative starts the next one with `- ` at the same level. Enter on a line that holds only `- ` removes it and leaves the variation, so no bare `-` is left behind. Tab on that line turns it into a continuation of the alternative above, indented under it. Typing `- ` on a continuation line moves it out to its variation's alternatives, since a `- ` there would otherwise be narration. A line counts as an alternative only when its `- ` sits directly in a `@sequence` or `@cycle` block; anywhere else a `- ` line is text, and Enter behaves as usual. These need code, since Enter rules can add text to the new line but can't remove the `- ` from the line being left.
   - **Closing spans.** Typing `[/` inside an open span completes the close for the innermost one, so `[wave]Ominously.[/` becomes `[wave]Ominously.[/wave]`. Spans open and close on the same line, so the script only reads the text before the cursor, and needs no language server.
3. **Language client (Phase 6),** which starts `pibbles lsp`.

**Toolchain.** The extension is built with Node, in its own folder, so `dotnet test` never needs it. The grammar is written in YAML, `syntaxes/pibbles.tmLanguage.yaml`, and `npm run build` compiles it to the JSON that VS Code reads. The editing aids are TypeScript in `src/`, which `npm run build` also compiles, with `tsc`, to CommonJS in `out/`. `extension.ts` only connects them to VS Code; their logic lives in modules that don't import `vscode`, so `npm test` runs their unit tests in `tests/unit/` with `node --test`, which runs TypeScript directly, with no editor or display. `npm test` also checks the grammar with `vscode-tmgrammar-test`, which runs VS Code's own TextMate engine over `.pib` files whose comments mark the scope each token should get, and a completeness check fails if a scope the grammar defines appears in no test. `npm run package` builds the `.vsix` with `vsce`. Install it with `code --install-extension pibbles-<version>.vsix`.

Until the language server exists, the extension contributes default settings for the [Code Spell Checker](https://marketplace.visualstudio.com/items?itemName=streetsidesoftware.code-spell-checker) extension, if the writer has it installed: it enables checking for `.pib` files, skips `@` lines, speakers and poses, tags, comments, `[markup]` and `{points}` with regular expressions, and reads `words.txt` as a custom dictionary. Without extension code, the dictionary's path is fixed: `story/words.txt` in the workspace, the default story folder, so a project with a different `story` setting doesn't get it. The patterns only approximate the grammar and know nothing about actors, so this is a stopgap. The language server's [spell checking](#spell-checking) replaces it, and the defaults are removed once it ships. Words added in the meantime carry over, since both read the same `words.txt`.

Writers will use VS Code, not Godot's script editor, for `.pib` files. Godot's editor has no extension point for custom language servers. The Godot adapter covers the in-engine side by printing diagnostics to the output panel on load.

## Language server

The language server is a thin layer over the `SemanticModel` ([semantics design](semantics.md#passes)). On every change it reparses the edited file and re-binds the project. Features, in priority order:

1. **Diagnostics,** published per file. A missing line ID (PIB3010) only shows on lines that haven't changed since the file was last saved ([line IDs in the editor](#line-ids-in-the-editor)).
2. **Completion:** actors after line start, poses inside `(`, commands after `@`, parameters and enum members in arguments, node names after `@jump`/`@call` (in relative form for nodes under the file's prefix), variables after `$`, markup after `[`, icons after `{icon `, tags after `#`.
3. **Go to definition:** jump targets, variables, commands, actors, markup.
4. **Hover:** declaration signatures, with the declaration's `///` notes as documentation, and diagnostic explanations from the catalog.
5. **Find references and rename:** for every declared name. Rename stops at a line boundary and never rewrites dialogue. Renamed nodes always get a `#was:` alias, and so do renamed variables once [release manifests](#with-extensions) arrive (the `pibbles.rename.aliases` setting turns this off). Renaming a file's `@prefix` renames each of its relative-named nodes, so each gets an alias. For names the game's code uses, the preview warns that the code must change too ([semantics design](semantics.md#pib5031--naming-convention)).
6. **Code actions:** extract selected statements to a new node ([semantics design](semantics.md#extract-to-node)), add missing line IDs (also on every save, by default), generate a migration header covering every retired ID that has nowhere to go (using node and range claims where possible), give a pasted duplicate a fresh ID, escape a colon that looks like a speaker, apply "did you mean" fixes, add an unknown word to `words.txt` ([spell checking](#spell-checking)).
7. **Semantic tokens,** to refine TextMate highlighting where the grammar can't tell things apart, such as a known versus unknown actor.
8. **Stretch:** document outline (nodes), folding, and a hover preview that renders a line's resolved text with its markers.

**LSP library:** we'll evaluate this in Phase 6. The options are `OmniSharp.Extensions.LanguageServer`, or `StreamJsonRpc` with a hand-written subset of protocol types. Only diagnostics, completion, definition, hover, references, rename, code actions and semantic tokens are needed, and that subset is small enough that a hand-written protocol layer is realistic if the library turns out heavy or unmaintained. The server lives in its own project, so the core never picks up the dependency.

### Line IDs in the editor

Writers never type line IDs, and the editor never makes them look at a warning for one they couldn't have yet.

- **IDs are added on every save,** the same way `pibbles ids` adds them. The `pibbles.ids.onSave` setting turns this off, for writers who'd rather run `pibbles ids` themselves.
- **PIB3010 only shows on lines that haven't changed since the file was last saved.** A line being written has no ID yet by design, and people go a long time between saves, so warnings on every new line would bury the warnings and errors that matter. With IDs added on save, a line that's unchanged since the last save always has one, so PIB3010 only appears in the editor when `pibbles.ids.onSave` is off.
- **With `pibbles.ids.onSave` off,** the editor says so once, when a file with missing IDs is saved, and suggests turning it back on or running `pibbles ids`.
- **`pibbles check` and CI aren't affected.** They see every line, and report every missing ID.

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
2. **`words.txt` in the first story folder.** A committed plain-text file with one word per line, optional. The "Add to dictionary" code action creates it or appends to it, keeping it sorted so parallel additions merge cleanly.
3. **Declared names.** Actor display names and persona names (`name: Mira`) are added automatically. A name is rarely misspelled where it's declared, so this also makes "did you mean" suggest `Mira` first for `Mirra`, using the same edit-distance suggestions as other diagnostics.

Hunspell dictionaries are read with [WeCantSpell.Hunspell](https://github.com/aarondandy/WeCantSpell.Hunspell), a pure .NET port. Its license, and the bundled `en` dictionary's, are confirmed in Phase 6 before depending on them.

Translations aren't checked yet. The same check can later run on each `<locale>.po` with that locale's dictionary and its own word list.

## Project settings

`pibbles.json` in the project root holds the facts about a project that aren't per file. The file and every key in it are optional, since each key has a default, and `pibbles init` writes it:

```json
{
  "schema": 2,
  "storyFolders": ["story"]
}
```

| Key | Meaning | Default |
| --- | --- | --- |
| `schema` | Which version of this format the file uses | `2` |
| `storyFolders` | The folders the story's `.pib` files are in, relative to the project root. Every one has to exist. | `["story"]` |

- **Only keys something uses are defined.** A later key arrives with the feature that reads it, with a default, and raises the schema by one: the version (for saves and release manifests), `sourceLocale` and `localization` (localization), `voice` (voice tooling) and `drafts` (drafts). `pibbles init` always writes the current schema.
- **An older schema still reads,** with each newer key at its default, and a renamed key read under its old name. Schema 1 named one folder as `"story": "story"`, which reads as `"storyFolders": ["story"]`. A key used under a schema that doesn't have it is an error that says which name to use.
- **Upgrading is opt-in.** A command that reads an older schema adds a note on standard error that `pibbles upgrade` updates the file, and otherwise carries on as usual, with the same output and exit code. Only `pibbles upgrade` rewrites the file, since a project's settings are the writer's to change, and a repository may need to stay readable by an older Pibbles for a while.
- **A newer schema than the tools know** is an error that asks the writer to update Pibbles, rather than a file misread.
- **An unknown key is an error** that lists the settings, so a typo such as `stroy` is never silently ignored.

## Configuration

Diagnostics are configured in `.editorconfig`, the way Roslyn analyzers are in .NET. Settings go in a `[*.pib]` section. They can live in the repository's existing `.editorconfig`, or in one under `story/`, and nested files override them per folder. A source file's settings come from the `.editorconfig` files in its folder and every folder above it, up to one with `root = true`, the way editors find them, so a repository's file applies even when the Pibbles project is a folder inside it:

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

- **Standard keys are respected.** `indent_style` and `indent_size` are what PIB5032 checks against. Without them, PIB5032 only checks that a file is consistent with itself. PIB1001 is about a file mixing tabs and spaces, whatever the setting, and `indent_style` only decides which one its help says to use.
- **A specific code beats its category,** wherever each is set, so a code's setting in a repository's root file still beats a category's setting in a folder's file, as in Roslyn. Otherwise a nearer file beats a farther one, and a later section beats an earlier one, following the usual `.editorconfig` rules.
- **A `pibbles_diagnostic` setting that names no code or category, or gives a severity that doesn't exist,** is reported once and ignored, so a typo is never silently dropped, and so is a threshold that isn't a whole number above 0. Other settings belong to other tools, so they're kept and ignored.
- **What stays in `pibbles.json`:** project facts that aren't per file, such as the version, source locale, localization and voice. `pibbles.json` describes what the project *is*. `.editorconfig` describes how strictly each file is checked.
- **The core stays free of I/O and of packages.** It doesn't read or parse `.editorconfig` files: each host resolves a source file's properties with an `.editorconfig` library and passes them in as `FileSettings`, in `CompilationOptions`, the same way sources are passed ([architecture](architecture.md#solution-layout)). The CLI uses [editorconfig-core-net](https://github.com/editorconfig/editorconfig-core-net) (`EditorConfig.Core`), the reference .NET implementation, and the language server can give it unsaved files through its `IFileSystem`.

## Releases

Every artifact ships in one release at one version: the CLI's `.nupkg` and the VS Code extension's `.vsix`, and later the Godot addon. The pieces change together, since a language change touches the parser and the grammar at once, so a writer always gets a CLI and an editor that agree on the language.

`release.yml` runs on a `v*` tag (`v0.2.0`), or by hand with a version. The version comes from the tag alone: it's passed to `dotnet pack` and `npm version`, so no file in the repository is bumped, and `pibbles --version` reports exactly that version. The workflow runs the .NET tests and the grammar tests, packs both artifacts, and attaches them to a draft GitHub release named `Pibbles <version>`, which is published by hand once it's been checked. Nothing is published to NuGet or the VS Code Marketplace.

Between releases, every CI run uploads the same artifacts with a prerelease version.

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
