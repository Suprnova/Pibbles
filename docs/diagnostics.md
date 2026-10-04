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
| PIB1013 | Error | I can't find the `{/if}` that ends this `{if}`. | Close it on the same line. |
| PIB1014 | Error | I don't know the escape `\n`. | A backslash only goes before punctuation. To show a backslash, write `\\`. |
| PIB1015 | Error | This text comes after a tag, but tags go at the end of the line. | If `#winning` is part of the text, put a backslash before the `#`: `\#winning`. |
| PIB1016 | Error | An option's text can't contain `{w}`. | Pauses and commands go in the indented lines under the option. |
| PIB1017 | Error | I don't know what `{name}` means. | To show a variable, write `{$name}`. To call a function, write `{name()}`. If the braces are part of the text, put a backslash before the `{`. |
| PIB1020 | Error | `@prefix` has to come first in the file. | Move it to the top. Only comments can go above it. |
| PIB1021 | Error | Declarations have to come before the file's first node. | Move this above the first `==` line, or into another file. |
| PIB1022 | Error | This line isn't inside a node. | Add a node header above it: `== name`. |
| PIB1023 | Error | This file already has a `@prefix`, on line 3. | A file has one prefix at most. To put nodes under two prefixes, split the file in two. |
| PIB1030 | Error | This `@else` has no `@if` to belong to. | Put it right after the `@if` block, at the same indentation as the `@if`. |
| PIB1031 | Error | A `@cycle:` block can only hold alternatives. | Start each alternative with `- `. |
| PIB1032 | Error | `@if` can't start an alternative. | Write the alternative's first line after the `- `, and put the `@if` on the line below it, indented. |
| PIB1033 | Error | I expected a `:` at the end of this `@if` line. | Add it: `@if $door_open:`. |
| PIB1040 | Error | I didn't expect `extra` here. | — |
| PIB1041 | Error | This quoted text never ends. | Add the closing `"` on the same line. |
| PIB1042 | Error | `1e5` isn't a number I can read. | Write numbers with digits and at most one `.`, like `3`, `0.5` or `.5`. A duration ends in `s` or `ms`, like `0.5s`. |
| PIB1043 | Error | `#show-disabled` isn't a tag name: tag names only have letters, digits and `_`. | Write `#show_disabled`. |
| PIB1044 | Error | A display name can't contain `[`, `{` or `\`. | Write the name as plain text. |
| PIB1045 | Error | `kitchen.door` has a dot, but only node names can. | Use a single name, like `kitchen_door`. |
| PIB1046 | Error | I expected a node name after `@jump`. | — |
| PIB1050 | Error | This argument has no name, but it comes after one that does. | Put unnamed arguments first, then named ones, then `wait` or `nowait`. |
| PIB1051 | Error | There's a space between `has_item` and its `(`. | To call `has_item`, remove the space: `has_item(…)`. |
| PIB1052 | Error | `#id:K7` isn't a line ID I can use. | Line IDs are lowercase letters, digits and `_`, starting with a letter. `pibbles ids` makes them for you. |
| PIB1053 | Error | `#thought` can't go on a node header. | A node header only takes `#was:` tags. Put other tags on the lines inside the node. |
| PIB1054 | Error | `mira:` has nothing after it. | Write what mira says after the colon, or write `mira: {w}` for a box with only their name. To change their pose without a line, write `mira (pose):`. |
| PIB1055 | Error | A negative argument has to go in brackets. | Write `(-1)`. |
| PIB1060 | Error | I can't find the `)` that closes this `(`. | Add the `)` on the same line. |
| PIB1061 | Error | I can't compare three things at once: `$a < $b < $c`. | Compare two at a time: `$a < $b and $b < $c`. |
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

### PIB1003

**Only some lines can have indented lines under them:** a line ending in `:` (`@if`, `@else`, `@once:` and the other block openers), an option (`->`) and an alternative (`- `). An indented line anywhere else is reported once, on the first line of the block, and the block's lines are read as if they weren't indented.

```text
@jump kitchen.leave
    mira: Bye!
```

Remove the indentation, or put the lines under a line that opens a block.

### PIB1004

**A line ending in `:` opens a block, so it needs indented lines under it.** That covers `@if`, `@elif`, `@else` and the other block openers. A comment or a `///` line on its own doesn't count, since it doesn't run.

```text
@if $door_open:
mira: It's open!
```

Indent the lines the opener controls. If it has nothing to do yet, put a placeholder line under it, or remove the opener.

### PIB1005

**`///` is reserved for notes to translators, which arrive with localization.** Until then, a line starting with `///` is an error, so stories written now can't already be using it for something else. A line starting with four or more slashes, such as a `////////` banner, is an ordinary comment.

```text
/// Mira is joking here.
mira: I live in the walls.
```

Write it as an ordinary comment for now: `// Mira is joking here.`

### PIB1010

**A markup span closes on the same line it opens on,** with `[/name]`. It also has to close inside the same conditional branch: a span opened inside `{if}…{/if}` closes before the branch ends, and one opened outside closes outside. An unclosed span is treated as running to the end of the line, or of its branch.

```text
mira: That's [clue]the master key.
```

Close it where the styling should end: `mira: That's [clue]the master key[/clue].`

### PIB1011

**Spans close innermost first,** like brackets: `[b][i]…[/i][/b]`, never `[b][i]…[/b][/i]`. The span that was opened last has to close first. The close is still read as closing both, so nothing else is reported.

```text
mira: [b][i]Both.[/b][/i]
```

Swap the closes: `mira: [b][i]Both.[/i][/b]`.

### PIB1012

**A `[/name]` has to close a span that's open at that point.** Spans don't cross conditional branches, so a close inside `{if}…{/if}` can't close a span opened before it.

```text
mira: That's it.[/b]
```

Remove the close, or add the `[b]` it belongs to. If the brackets are meant as text, escape the first one: `\[/b]`.

### PIB1013

**A `{`, a `[` or an `{if}` has to close on the same line,** with `}`, `]` or `{/if}`. The message points at the opener that's still open.

```text
rex: {if $bravery > 2}Maybe stop hitting it?
```

Add the closer where it belongs: `rex: {if $bravery > 2}Maybe stop hitting it?{/if}`.

### PIB1014

**A backslash makes the punctuation after it literal, in text and in quoted strings.** `\#` writes a `#` instead of starting a tag, and `\"` writes a quote inside quoted text. Only ASCII punctuation can follow a backslash. A letter, digit or space after one is an error, and so is a backslash at the end of a line, so that escapes like `\n` can be given a meaning later without changing any story.

```text
@set $path = "C:\Users\mira"
```

To show a backslash itself, write two: `"C:\\Users\\mira"`.

### PIB1015

**Tags are labels for the game, and they go at the end of a line,** after its text and an option's modifiers. A `#` followed by a letter always starts a tag, so text after one is reported.

```text
mira: I'm #winning today.
```

If the `#` is part of the text, put a backslash before it: `mira: I'm \#winning today.` A `#` followed by anything other than a letter, as in `my #1 fan`, needs no escape.

### PIB1016

**An option's text is shown in a menu, so it can't pause or run commands.** `{w}`, `{p}` and `{@command}` belong in the lines under the option, which run after it's picked. Markup, `{$variables}`, icons, `{br}` and `{if}` are all fine in option text.

```text
-> Knock{w} and wait
```

Move the pause into the option's body:

```text
-> Knock and wait
    {w}
```

### PIB1017

**Curly braces hold a point:** `{$variable}`, `{function()}`, `{@command}`, or one of `{w}`, `{p}`, `{br}`, `{icon name}` and `{if}`. Anything else is reported, including a bare name, which may get a meaning in a later version.

```text
mira: Hi, {name}.
```

Write `{$name}` for a variable, or `{name()}` for a function. If the braces are meant as text, escape the first one: `\{name}`.

### PIB1020

**`@prefix` sets the group name for a whole file, so it comes first, after nothing but comments.**

```text
@var $has_key = false
@prefix kitchen
```

Move `@prefix` above everything else. A second `@prefix` in the same file is [PIB1023](#pib1023) instead.

### PIB1022

**Everything a story shows or does belongs to a node.** Before a file's first node (`== name`), only `@prefix`, comments and declarations can appear.

```text
mira: Hello!

== kitchen.enter
```

Add a node header above the line, or move the line into an existing node.

### PIB1023

**A file has one prefix at most,** because the prefix is what every relative name in the file (`.door`) is relative to. A second `@prefix` is reported wherever it appears, and the message names the line of the first one.

```text
@prefix kitchen

== .door
mira: Locked.

@prefix cellar
```

To keep nodes under two group names, split the file in two, each with its own `@prefix`. A node can also use its full name (`== cellar.stairs`) in a file whose prefix is something else.

### PIB1030

**`@elif` and `@else` continue an `@if`, so they come right after its block, at the same indentation.** Comments between them are fine. Anything else in between ends the `@if`, and so does an earlier `@else`. The statements under a stray `@elif` or `@else` are still read, as part of the block around it.

```text
@if $door_open:
    mira: It's open!
mira: Hm.
@else:
    mira: Still locked.
```

Move the clause up to just after the `@if` block, or indent it to match the `@if` it belongs to.

### PIB1033

**`@if`, `@elif` and `@else` end with a `:`,** which says a block follows. The block under the line still belongs to it, so this is the only problem reported.

```text
@if $door_open
    mira: It's open!
```

Add the `:`: `@if $door_open:`.

### PIB1040

**A line has something I can't read where it is.** This is the general code, for leftovers that no more specific code describes: an extra word after a statement, a node header indented inside a block, and so on.

```text
@jump kitchen.leave now
```

Remove what's extra. If a word belongs to the next line, move it there.

### PIB1041

**Quoted text has to end with a `"` on the same line it starts on.** Everything after an opening `"` is part of the text until the closing one, so without it, the rest of the line becomes text too.

```text
@enter_room "kitchen on_exit=kitchen.leave
```

Add the closing `"` where the text should end: `@enter_room "kitchen" on_exit=kitchen.leave`. To put a quote inside quoted text, write `\"`.

### PIB1042

**A number is digits with at most one `.`, and a duration is a number followed by `s` or `ms`.** `3`, `0.5`, `.5`, `1.`, `0.5s` and `300ms` are all fine. Exponents (`1e5`) and digit separators (`1_000`) aren't supported, and anything written directly after a number, other than `s` or `ms`, is reported as part of it.

```text
@wait 2sec
```

Write `@wait 2s`.

### PIB1043

**A tag name holds only letters, digits and `_`, and starts with a letter.** A tag's value, after the `:`, can hold anything except spaces.

```text
-> Use the key  @if $has_key #show-disabled
```

Write `#show_disabled`, and declare it that way with `@tag`.

### PIB1046

**A line is missing a part it needs,** such as the node name after `@jump`, `@call`, `==`, `@prefix` or `#was:`.

```text
@jump
```

Write the missing part: `@jump kitchen.leave`.

### PIB1050

**A command's unnamed arguments come first, in order, then its named ones (`name=value`), then `wait` or `nowait`.** Named arguments can go in any order among themselves.

```text
@show at=left mira
```

Write `@show mira at=left`.

### PIB1051

**A call's `(` touches its name:** `has_item("key")`. With a space, I read the name and the brackets as two separate things. Where only a call makes sense, as in a condition or an `@set`, the space is reported. In a command's arguments, a space really does separate two arguments: `@give item ("key")` passes `item` and `"key"`.

```text
@if has_item ("key"):
```

Remove the space: `@if has_item("key"):`.

### PIB1053

**Some lines only take certain tags.** A node header only takes `#was:`, which records the node's old names. The host never reads a node header, so other tags would have no effect there.

```text
== kitchen.fridge #thought
```

Put the tag on the line inside the node that it belongs to.

### PIB1054

**A speaker's name and colon need something after them:** text to say, or a pose to change to. A bare `mira:` is reported, since it's most likely unfinished.

```text
mira:
```

Write what Mira says, or `mira (happy):` to change her pose without a line. For a message box that shows only her name, write `mira: {w}`, which waits for the player and shows no text.

### PIB1055

**A command's arguments are separated by spaces, so an operator inside one needs brackets, and so does a minus sign.** Without them, `@shake_screen -1` could also be read as two things.

```text
@shake_screen -1
```

Write `@shake_screen (-1)`. A negative value elsewhere, as in `@set $x = -1`, needs no brackets.

### PIB1060

**Every `(` needs a `)` on the same line.** The message points at the `(` that's still open.

```text
@if has_item("key":
```

Add the `)` where the brackets should close: `@if has_item("key"):`.

### PIB1061

**Comparisons don't chain.** In some languages `$a < $b < $c` means "`$b` is between `$a` and `$c`", and in others it quietly compares `true` or `false` with `$c`. Pibbles asks you to say which you mean.

```text
@if 0 < $bravery < 3:
```

Write each comparison out and join them with `and`: `@if 0 < $bravery and $bravery < 3:`. If you really mean to compare the result of one comparison, put it in brackets: `@if ($a < $b) == $c:`.

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
