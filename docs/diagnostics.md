# Diagnostics

The catalog of every diagnostic code. Each code has one entry here and one in `DiagnosticCatalog`, which the code registers it in. The table's message and help are what the CLI and the language server show ([how diagnostics read](syntax.md#how-diagnostics-read)). As a code is implemented, it also gains a section under [Explanations](#explanations): what it means, an example that triggers it, and how to fix it. `pibbles explain <code>` prints that section, and language-server hovers show it.

## Ranges

| Range | Stage | Category in `.editorconfig` |
| --- | --- | --- |
| `PIB1xxx` | Syntax ([syntax design](syntax.md)) | `syntax` |
| `PIB2xxx` | Declarations and binding ([semantics design](semantics.md)) | `binding` |
| `PIB3xxx` | Flow and content checks ([semantics design](semantics.md)) | `content` |
| `PIB4xxx` | Localization ([localization design](localization.md)) | `localization` |
| `PIB5xxx` | Style ([style rules](semantics.md#style-rules)) | `style` |
| `PIB6xxx` | Spelling ([spell checking](tooling.md#spell-checking)) | `spelling` |

Severities are error, warning, info and hint. `.editorconfig` can override any of them ([configuration](tooling.md#configuration)).

"Did you mean" suggestions use edit distance against the relevant symbol kind. They're cheap to add and help a lot with the typos a branching script is most exposed to.

## Writing messages

Most people who read a diagnostic are writers, not programmers. Each catalog entry has a **message**, an optional **label** shown under the marked text, and an optional **help** ([how diagnostics read](syntax.md#how-diagnostics-read)).

- **The message says what's wrong, in one plain sentence.** Pibbles speaks as "I", and the reader is "you": *I can't find the end of this `[clue]`.*
- **No compiler words.** Never "token", "parse", "syntax", "statement", "production", "INDENT" or "invalid". "Unexpected" only says something when the message also says what was expected.
- **Quote the reader's own text,** with their names in it, rather than describing it.
- **The help is a fix, not a rule.** Where a fix exists, write out the corrected text. "Did you mean" suggestions go here.
- **One problem, one diagnostic.** A message never depends on another one to make sense.

## Codes

| Code | Default | Message | Help |
| --- | --- | --- | --- |
| PIB1001 | Error | This file indents with both tabs and spaces, so I can't tell which block this line belongs to. | Use only spaces or only tabs in a file. Most editors can convert the whole file for you. |
| PIB1002 | Error | I can't tell which block this line belongs to: its indentation doesn't line up with any block above it. | Indent it to match the block it belongs to. |
| PIB1003 | Error | This line is indented, but the line above it doesn't open a block. | Only a line ending in `:`, an option (`->`) or an alternative (`- `) can have indented lines under it. |
| PIB1004 | Error | I expected indented lines under `@if $door_open:`. | Put the lines it controls below it, indented. |
| PIB1005 | Error | `///` notes aren't supported yet. | For a comment, use `//`. |
| PIB1010 | Error | I can't find the end of this `[clue]`. | Close it on the same line with `[/clue]`. |
| PIB1011 | Error | I expected `[/i]` here, because `[i]` was opened last. | Close markup in the reverse order you opened it: `[b][i]…[/i][/b]`. |
| PIB1012 | Error | `[/b]` closes markup that was never opened. | If you meant the text `[/b]`, put a backslash before it: `\[/b]`. |
| PIB1013 | Error | I can't find the `{/if}` that ends this `{if}`. | Conditional text ends on the same line, with `{/if}`. |
| PIB1014 | Error | I don't know the escape `\n`. | A backslash only goes before punctuation. To show a backslash, write `\\`. |
| PIB1015 | Error | This text comes after a tag, but tags go at the end of the line. | If `#winning` is part of the text, put a backslash before the `#`: `\#winning`. |
| PIB1016 | Error | An option's text can't contain `{w}`. | Pauses and commands go in the indented lines under the option. |
| PIB1017 | Error | I don't know what `{name}` means. | To show a variable, write `{$name}`. To call a function, write `{name()}`. |
| PIB1020 | Error | `@prefix` has to come first in the file. | Move it to the top. Only comments can go above it. |
| PIB1021 | Error | Declarations have to come before the file's first node. | Move this above the first `==` line, or into another file. |
| PIB1022 | Error | This line isn't inside a node. | Add a node header above it: `== name`. |
| PIB1030 | Error | This `@else` has no `@if` to belong to. | Put it right after the `@if` block, at the same indentation as the `@if`. |
| PIB1031 | Error | A `@cycle:` block can only hold alternatives. | Start each alternative with `- `. |
| PIB1032 | Error | `@if` can't start an alternative. | Write the alternative's first line after the `- `, and put the `@if` on the line below it, indented. |
| PIB1033 | Error | I expected a `:` at the end of this `@if` line. | Add it: `@if $door_open:`. |
| PIB1040 | Error | I didn't expect `)` here. | Check for a missing `(` or an extra `)`. |
| PIB1041 | Error | This quoted text never ends. | Add the closing `"` on the same line. |
| PIB1042 | Error | `.5` isn't a number I can read. | Write it as `0.5`. |
| PIB1043 | Error | `#show-disabled` isn't a tag name: tag names only have letters, digits and `_`. | Write `#show_disabled`. |
| PIB1044 | Error | A display name can't contain `[`, `{` or `\`. | Write the name as plain text. |
| PIB1045 | Error | `kitchen.door` has a dot, but only node names can. | Use a single name, like `kitchen_door`. |
| PIB1050 | Error | This argument has no name, but it comes after one that does. | Put unnamed arguments first, then named ones, then `wait` or `nowait`. |
| PIB1051 | Error | I read `has_item ("key")` as two separate things, because of the space before `(`. | To call `has_item`, remove the space: `has_item("key")`. |
| PIB1052 | Error | `#id:K7` isn't a line ID I can use. | Line IDs are lowercase letters, digits and `_`, starting with a letter. `pibbles ids` makes them for you. |
| PIB1053 | Error | A node header can only have `#was:` tags. | Move other tags to the lines inside the node. |
| PIB1054 | Error | `mira:` has nothing after it. | Write what Mira says after the colon, or change her pose with `mira (happy):`. If this is narration, escape the colon: `mira\:`. |
| PIB1055 | Error | I can't compare three things at once: `$a < $b < $c`. | Compare two at a time: `$a < $b and $b < $c`. |
| PIB2001 | Error | I don't know an actor called `Note`. | If this line is narration, escape the colon: `Note\:`. |
| PIB2002 | Error | Mira has no pose called `smirk`. | Did you mean `smug`? |
| PIB2003 | Error | `(to Rex)` isn't a pose: a pose is a single name. | For how a line is said, use a `//` comment. If this is narration, escape the colon: `mira (to Rex)\:`. |
| PIB2004 | Warning | There's no space after `mira:`. | Add one, or escape the colon if this is narration: `mira\:Hi`. |
| PIB2010 | Error | I don't know a command called `@shak`. | Did you mean `@shake_screen`? |
| PIB2011 | Error | `@show` expects a `position` for `at`, but this is a `number`. | Use one of `left`, `center`, `right` or `offscreen`. |
| PIB2012 | Error | `@give_item` can't be used inside a line, because it isn't declared `inline`. | Put it on its own `@` line. |
| PIB2020 | Error | I can't find a node called `kitchen.dor`. | Did you mean `kitchen.door`? |
| PIB2021 | Error | `.leave` is relative, but this file has no `@prefix`. | Write the full name, or add a `@prefix` at the top of the file. |
| PIB2030 | Error | I don't know a variable called `$has_kye`. | Did you mean `$has_key`? |
| PIB2045 | Error | I don't know a tag called `#thougth`. | Did you mean `#thought`? If this is text, escape it: `\#thougth`. |
| PIB2046 | Error | `#thought` doesn't take a value. / `#box` needs a value. | Write `#thought` on its own. / Write a value after the colon: `#box:phone`. |
| PIB2047 | Error | `kitchen.front_door` is an old name of `kitchen.door`. | Use the current name. |
| PIB3001 | Warning | This line never runs, because of the `@jump` above it. | Remove it, or move it above the `@jump`. |
| PIB3010 | Warning | This line has no `#id`. | Run `pibbles ids` to add one. |
| PIB3011 | Error | The line ID `k7qp2x` is also used at rooms/cellar.pib:40. | Delete one of the two IDs and run `pibbles ids` to give that line a new one. |

The style rules, PIB5001 to PIB5040, are documented with examples in the [semantics design](semantics.md#style-rules).

## Explanations

One section per implemented code, headed `### PIB1001`. The CLI embeds this file at build time, so `pibbles explain` and the docs never drift apart.

### PIB1001

**Indentation decides which lines belong to an `@if`, an option or a variation, so a file has to indent the same way throughout.** A tab and a space look different widths in different editors, so Pibbles doesn't guess how many spaces a tab is worth. The first indented line decides how the whole file indents, and every other indented line has to use the same character. Comment lines and blank lines don't count, because their indentation never matters.

```text
@if $door_open:
    mira: It's open!
@else:
→   mira: Still locked.
```

The first indented line uses spaces, so the tab (shown as `→`) on the last line is reported. Re-indent the file with one character: most editors have a command for converting tabs to spaces, or spaces to tabs. Until it's fixed, a line indented with the other character still counts by its width, with a tab counting as one character.

### PIB1002

**A line that steps back out of a block has to line up with a block it's stepping back to.**

```text
@if $door_open:
        mira: It's open!
    mira: Hm.
```

`mira: Hm.` is indented less than the line above, so it leaves the `@if` block, but four spaces lines up with no block above it: the `@if` is at zero and its block is at eight. Pibbles treats the line as part of the outer block, the one at zero. Indent it to match the block it belongs to: zero to come after the `@if`, or eight to be part of it.

### PIB1005

**`///` is reserved for notes to translators, which arrive with localization.** Until then, a line starting with `///` is an error, so stories written now can't already be using it for something else. A line starting with four or more slashes, such as a `////////` banner, is an ordinary comment.

```text
/// Mira is joking here.
mira: I live in the walls.
```

Write it as an ordinary comment for now: `// Mira is joking here.`

## With extensions

Codes that arrive with the [grammar extensions](language/design.md#grammar-extensions) and later tiers ([roadmap](roadmap.md#feature-tiers)).

| Code | Arrives with | Default | Example message |
| --- | --- | --- | --- |
| PIB2035 | Terms | Error | Term `they` refers to itself through `them` |
| PIB2040 | Personas | Error | Actor `rex` declares persona `stranger` twice |
| PIB2050 | `random()` | Error | `random()` can't be used in inline text, because it would roll again when a save loads. Roll it into a variable with `@set` first. (The same code covers terms, option conditions and persona conditions.) |
| PIB3020 | Release manifests | Error | Saves from 1.0 at `k7qp2x` have nowhere to go. Add `#was:k7qp2x` to a line, or a migration. |
| PIB3021 | Release manifests | Warning | Saves from 1.0 at `h8ya3k` resume after the new `@set $has_key` above it. Add a migration for 1.0 if they need it. |
| PIB3022 | Migrations | Error | `migrate.a` and `migrate.b` both claim `k7qp2x` with equally specific claims |
| PIB3023 | Migrations | Error | `#migrates:cellar.old_subplt` matches nothing in any recorded release |
| PIB3024 | Release manifests | Warning | Saves from 1.0 at `k7qp2x` resume in `cellar.stairs`, which `kitchen.door` no longer leads to |
| PIB3025 | Release manifests | Warning | Variable `$HasKey` shipped in 1.0 no longer exists. If it was renamed, add `#was:$HasKey` to its `@var`. |
| PIB3030 | Drafts | Warning | `kitchen.door` jumps to the draft node `kitchen.new_scene`, which release builds leave out |
| PIB4001 | Localization (Phase 5) | Error | Translation `es` drops the effect `{@sfx door_rattle}` |
| PIB4010 | Localization (Phase 5) | Warning | Translation `es` is stale: the source text changed |
| PIB4020 | Voice tooling (stretch) | Info/Warning | Recording for `k7qp2x` may be out of date: 'teh' → 'the' |
| PIB6001 | Spell checking (Phase 6) | Info | Unknown word `Mirra`. Did you mean `Mira`? |
