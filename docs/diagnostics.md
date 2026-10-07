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

Within a range, each decade is one group of related codes, such as PIB2000–2009 for speakers and poses, and the [table below](#codes) shows the groups. A new code takes the next free number in its group, and a full group continues in a later free decade. A code is never renumbered once it ships, and the codes the [extensions](#with-extensions) reserve stay free.

Severities are error, warning, info and hint. `.editorconfig` can override any of them ([configuration](tooling.md#configuration)).

"Did you mean" suggestions use edit distance against the relevant symbol kind. They're cheap to add and help a lot with the typos a branching script is most exposed to. A suggestion is the closest name, ignoring case, within `max(1, length / 3)` edits, where an edit inserts, deletes or replaces a letter, or swaps two letters side by side (`thougth` is one edit from `thought`). Ties go to the name declared first. When nothing is close enough, the help is left out.

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
| PIB1003 | Error | This line is indented, but the line above it doesn't open a block. | Only a block opener such as `@if`, an option (`->`) or an alternative (`- `) can have indented lines under it. |
| PIB1004 | Error | I expected indented lines under `@if $door_open`. | Put the lines it controls below it, indented. |
| PIB1005 | Error | `///` notes aren't supported yet. | For a comment, use `//`. |
| PIB1010 | Error | I can't find the end of this `[clue]`. | Close it on the same line with `[/clue]`. |
| PIB1011 | Error | I expected `[/i]` here, because `[i]` was opened last. | Close markup in the reverse order you opened it: `[b][i]…[/i][/b]`. |
| PIB1012 | Error | `[/b]` closes markup that was never opened. | If you meant the text `[/b]`, put a backslash before it: `\[/b]`. |
| PIB1013 | Error | I can't find the `{/if}` that ends this `{if}`. | Close it on the same line. |
| PIB1014 | Error | I don't know the escape `\n`. | A backslash only goes before punctuation. To show a backslash, write `\\`. |
| PIB1015 | Error | This text comes after a tag, but tags go at the end of the line. | If `#winning` is part of what Mira says, put a backslash before the `#`: `mira: I'm \#winning today.` |
| PIB1016 | Error | An option's text can't contain `{w}`. | Pauses, speed changes and commands go in the indented lines under the option. |
| PIB1017 | Error | I don't know what `{name}` means. | To show a variable, write `{$name}`. To call a function, write `{name()}`. If the braces are part of the text, put a backslash before the `{`. |
| PIB1020 | Error | `@prefix` has to come first in the file. | Move it to the top. Only comments can go above it. |
| PIB1021 | Error | Declarations have to come before the file's first node. | Move this above the first `==` line, or into another file. |
| PIB1022 | Error | This line isn't inside a node. | Add a node header above it: `== name`. |
| PIB1023 | Error | This file already has a `@prefix`, on line 3. | A file has one prefix at most. To put nodes under two prefixes, split the file in two. |
| PIB1030 | Error | This `@else` has no `@if` to belong to. | Put it right after the `@if` block, at the same indentation as the `@if`. |
| PIB1031 | Error | A `@cycle` block can only hold alternatives. | Start each alternative with `- `. |
| PIB1032 | Error | `@if` can't start an alternative. | Put `-` on its own line, and the `@if` on the line below it, indented. |
| PIB1033 | Error | I didn't expect a `:` on this `@if` line. | Remove it: `@if $door_open`. |
| PIB1040 | Error | I didn't expect `extra` here. | — |
| PIB1041 | Error | This quoted text never ends. | Add the closing `"` on the same line. |
| PIB1042 | Error | `1e5` isn't a number I can read. | Write numbers with digits and at most one `.`, like `3`, `0.5` or `.5`. A duration ends in `s` or `ms`, like `0.5s`. |
| PIB1043 | Error | `#show-disabled` isn't a tag name: tag names only have letters, digits and `_`. | Write `#show_disabled`. |
| PIB1044 | Error | A display name can't contain `[`, `{` or `\`. | Write the name as plain text. |
| PIB1045 | Error | `kitchen.door` has a dot, but only node names can. | Use a single name, with `_` between words if it needs them. |
| PIB1046 | Error | I expected a node name after `@jump`. | — |
| PIB1047 | Error | `99999999999999999999999999999` is too large a number for me. | Numbers go up to 79228162514264337593543950335. Write a smaller one. |
| PIB1050 | Error | This argument has no name, but it comes after one that does. | Put unnamed arguments first, then named ones, then `wait` or `nowait`. |
| PIB1051 | Error | There's a space between `has_item` and its `(`. | To call `has_item`, remove the space: `has_item(…)`. |
| PIB1052 | Error | `#id:K7` isn't a line ID I can use. | Line IDs are lowercase letters, digits and `_`, starting with a letter. `pibbles ids` makes them for you. |
| PIB1053 | Error | `#thought` can't go on a node header. | A node header only takes `#was:` tags. Put other tags on the lines inside the node. |
| PIB1054 | Error | `mira:` has nothing after it. | Write what Mira says after the colon, or write `mira: {w}` for a box with only their name. To change their pose without a line, write `mira (pose):`. |
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
| PIB2013 | Error | `has_item()` takes only 1 argument. | Remove the extra ones. |
| PIB2014 | Error | `@show` has no parameter called `att`. | Did you mean `at`? |
| PIB2015 | Error | `near()` needs a value for `where`. | Add it in the brackets, in order. |
| PIB2016 | Error | `@show` already has a value for `who`. | Remove one of them. |
| PIB2017 | Error | I don't know markup called `clu`. | Did you mean `clue`? |
| PIB2018 | Error | I don't know an icon called `interct`. | Did you mean `interact`? |
| PIB2019 | Error | I don't know a function called `has_iten`. | Did you mean `has_item`? |
| PIB2020 | Error | I can't find a node called `kitchen.dor`. | Did you mean `kitchen.door`? |
| PIB2021 | Error | `.leave` is relative, but this file has no `@prefix`. | Write the full name, or add a `@prefix` at the top of the file. |
| PIB2022 | Error | There's already a node called `kitchen.door`, on line 12. | A node's name, and each old name in its `#was:`, can only be used once in a story. Rename one of them. |
| PIB2023 | Info | `@prefix .kitchen` doesn't need its dot. | Remove it: `@prefix kitchen`. |
| PIB2030 | Error | I don't know a variable called `$has_kye`. | Did you mean `$has_key`? |
| PIB2031 | Error | `$has_key` holds a `bool`, but this is a `number`. | Use `true` or `false`. |
| PIB2032 | Error | `$attempts` is a `number`, but a condition has to be true or false. | Did you mean `$attempts > 0`? |
| PIB2033 | Error | I can't use `<` with a `string` and a `string`. | `<`, `<=`, `>` and `>=` compare two numbers or two durations. |
| PIB2034 | Error | I don't know what `has_key` means here. | Did you mean `$has_key`? |
| PIB2036 | Error | `lfet` isn't a `position`. | Did you mean `left`? |
| PIB2037 | Error | I can't compare two names: `left == right`. | Compare a variable with a name instead, like `$where == left`. |
| PIB2038 | Error | I can't show `$has_key` in text, because it's a `bool`. | Show text that depends on it instead: `{if $has_key}…{else}…{/if}`. |
| PIB2039 | Error | A pause has to be more than zero, but `0` isn't. | Write a pause longer than zero, like `{w 0.5}`. |
| PIB2045 | Error | I don't know a tag called `#thougth`. | Did you mean `#thought`? If this is text, escape it: `\#thougth`. |
| PIB2046 | Error | `#thought` doesn't take a value. / `#box` needs a value. | Write `#thought` on its own. / Write a value after the colon: `#box:phone`. |
| PIB2047 | Warning | `kitchen.front_door` is an old name of `kitchen.door`. | Use the current name: `kitchen.door`. |
| PIB2048 | Error | `fone` isn't a `box_style`, which `#box` takes. | Use one of `phone` or `letter`. |
| PIB2060 | Error | There's already an enum called `room`, in story/cast.pib on line 8. | Give one of them another name, or remove one. |
| PIB2061 | Error | `happy` is already one of Mira's poses. | Remove the second `happy`, or give it another name. |
| PIB2062 | Error | `if` can't name a command, because Pibbles uses that word after `@`. | Choose another name. |
| PIB2063 | Error | `zoë` has `ë` in it, but a name can only use English letters, digits and `_`. | Replace `ë` with a letter from a to z. |
| PIB2064 | Error | I don't know a type called `postion`. | Did you mean `position`? |
| PIB2065 | Error | `duration` has no default, but it comes after a parameter that has one. | Put the parameters that have defaults last. |
| PIB2066 | Error | A tag's value is text, so it can't be `number`. | Use `string`, or an enum to allow only certain values. |
| PIB2067 | Error | `$where` starts as `left`, so I need its type written out. | Write the type after the variable: `@var $where: position = left`. |
| PIB2070 | Warning | `$total / 0` divides by zero, so it gives 0. | Divide by something that isn't zero. |
| PIB3001 | Warning | This line never runs, because of the `@jump` above it. | Remove it, or move it above the `@jump`. |
| PIB3002 | Info | This option has no text. | Write what the player picks after the `->`. |
| PIB3010 | Warning | This line has no `#id`. | Run `pibbles ids` to add one. |
| PIB3011 | Error | The line ID `k7qp2x` is also used in story/cellar.pib on line 40. | Delete one of the two IDs and run `pibbles ids` to give that line a new one. |
| PIB3012 | Error | The line ID `intro` is also the name of a node. | Delete the ID and run `pibbles ids` to give the line a new one. |
| PIB5001 | Hint | This line is 4 blocks deep, and more than 3 gets hard to follow. | Move the inner blocks into a node of their own and `@call` it, or combine conditions with `and`. |
| PIB5002 | Hint | This option's body is 16 lines long, more than 15, so the rest of the choice ends up far from it. | Move the body into a node of its own and `@jump` to it. |
| PIB5003 | Hint | Mira says this same line in 3 places. | Put it in a node of its own and `@call` that node from each place, so it's translated and recorded once. |
| PIB5004 | Hint | The color `#ff8800` is used in 2 places. | If it means something, such as a clue, give it a name: declare a markup such as `@markup clue`, ask whoever programs the game to style it, and use `[clue]` in each place. |
| PIB5010 | Hint | This pose change does nothing, because Mira already has that pose. | Leave the pose out of this line. |
| PIB5011 | Hint | This pause does nothing, because the end of the line already waits for a click. | Remove it. |
| PIB5012 | Hint | This `[b]` is empty. | Remove it. |
| PIB5013 | Hint | Every branch of this `@if` is the same, so the condition makes no difference. | Keep one copy of the branch, without the condition. |
| PIB5014 | Hint | This `@return` does nothing, since the end of a node returns anyway. | Remove it. |
| PIB5020 | Hint | Comparing with `true` isn't needed. | Write `$has_key`. |
| PIB5021 | Hint | This can be shorter with `+=`. | Write `@set $attempts += 1`. |
| PIB5030 | Hint | This line is 321 characters long, more than 300. | Split it into two lines, or into pages with `{p}`. |
| PIB5031 | Hint | `HasKey` isn't written in snake_case. | Write it as `has_key`, before anything outside the story uses the name. |
| PIB5032 | Hint | This block is indented 2 spaces, but the file's first block is indented 4 spaces. | Indent it 4 spaces. |
| PIB5033 | Hint | This speaker is spaced differently from the usual `mira (happy):`. | Write `mira (happy):`. |
| PIB5040 | Info | Nothing in the story uses `$old_flag`. | Remove its `@var`, or use it. |

The style rules (PIB5xxx) are described in full, with why each one exists and its quick fix, in the [semantics design](semantics.md#style-rules). A line with an error gets no style hints. A style hint is silenced for one line by a `// pibbles-ignore PIB5003` comment on its own line above it, or for a whole file by one above the file's first node.

## Explanations

One section per implemented code, headed `### PIB1001`. The CLI embeds this file at build time, so `pibbles explain` and the docs never drift apart.

### PIB1001

**Indentation decides which lines belong to an `@if`, an option or a variation, so a file has to indent the same way throughout.** A tab and a space look different widths in different editors, so Pibbles doesn't guess how many spaces a tab is worth. The first indented line decides how the whole file indents, and every other indented line has to use the same character. Comment lines and blank lines don't count, because their indentation never matters.

```text
@if $door_open
    mira: It's open!
@else
→   mira: Still locked.
```

The first indented line uses spaces, so the tab (shown as `→`) on the last line is reported. Re-indent the file with one character: most editors have a command for converting tabs to spaces, or spaces to tabs. When `indent_style` in `.editorconfig` says which one the project uses, the help names it. The setting never makes a file that's consistent with itself an error; a style hint (PIB5032) covers that. Until it's fixed, a line indented with the other character still counts by its width, with a tab counting as one character.

### PIB1002

**A line that steps back out of a block has to line up with a block it's stepping back to.**

```text
@if $door_open
        mira: It's open!
    mira: Hm.
```

`mira: Hm.` is indented less than the line above, so it leaves the `@if` block, but four spaces lines up with no block above it: the `@if` is at zero and its block is at eight. Pibbles treats the line as part of the outer block, the one at zero. Indent it to match the block it belongs to: zero to come after the `@if`, or eight to be part of it.

### PIB1003

**Only some lines can have indented lines under them:** a block opener (`@if`, `@else`, `@once` and the others), an option (`->`) and an alternative (`- `). An indented line anywhere else is reported once, on the first line of the block, and the block's lines are read as if they weren't indented.

```text
@jump kitchen.leave
    mira: Bye!
```

Remove the indentation, or put the lines under a line that opens a block.

### PIB1004

**A block opener needs indented lines under it.** That covers `@if`, `@elif`, `@else`, the variations and `@actor`. A comment or a `///` line on its own doesn't count, since it doesn't run.

```text
@if $door_open
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

**An option's text is shown in a menu, so it can't pause, change speed or run commands.** `{w}`, `{p}`, `{speed}` and `{@command}` belong in the lines under the option, which run after it's picked. Markup, `{$variables}`, icons, `{br}` and `{if}` are all fine in option text.

```text
-> Knock{w} and wait
```

Move the pause into the option's body:

```text
-> Knock and wait
    {w}
```

### PIB1017

**Curly braces hold a point:** `{$variable}`, `{function()}`, `{@command}`, or one of `{w}`, `{p}`, `{br}`, `{speed}`, `{icon name}` and `{if}`. Anything else is reported, including a bare name, which may get a meaning in a later version.

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

### PIB1021

**Declarations make up the contract between the story and the game, and they all come before a file's first node.** That keeps them easy to find, and keeps the cast list in one place. A file can hold only declarations.

```text
== kitchen.door
@var $door_open = false
```

Move the declaration above the first `==` line, or into the file that holds the story's other declarations.

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
@if $door_open
    mira: It's open!
mira: Hm.
@else
    mira: Still locked.
```

Move the clause up to just after the `@if` block, or indent it to match the `@if` it belongs to.

### PIB1031

**A `@sequence` or `@cycle` block holds only alternatives, each starting with `- `.** The block picks one alternative each time it's reached, so a line without a `- ` has no alternative to belong to. It's reported once per block, and each such line is read as an alternative of its own. `@once` is different: it holds ordinary lines.

```text
@cycle
    mira: Hm.
    mira: Huh.
```

Start each alternative with `- `: `- mira: Hm.`

### PIB1032

**After `- ` comes a single line: dialogue, narration, `@set`, a jump, `@wait` or a command.** A line that opens a block of its own, such as `@if`, a variation or an option, can't follow `- `, because its block and the alternative's continuation would be the same lines. The opener is still read with its block, so nothing else is reported.

```text
@cycle
    - @if $has_key
        mira: I could use the key.
```

Put the `-` on its own line, and the opener on the indented line below it:

```text
@cycle
    -
        @if $has_key
            mira: I could use the key.
```

### PIB1033

**A block opener has no `:`.** The keyword (`@if`, `@elif`, `@else`, `@sequence`, `@cycle`, `@once` or `@actor`) already says a block follows. The block under the line still belongs to it, so this is the only problem reported.

```text
@if $door_open:
    mira: It's open!
```

Remove the `:`: `@if $door_open`.

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

### PIB1044

**An actor's display name, after `name:`, is plain text.** `[`, `{` and `\` are reserved there, so that display names can hold markup in a later version without changing any story. Everything else, including `#`, `//` and quotes, is part of the name.

```text
@actor mira
    name: Mira [the brave]
```

Write the name as plain text: `name: Mira the Brave`.

### PIB1045

**Names you declare are single names:** actors, poses, enums and their members, variables' types, commands, markup, icons, tags, functions and parameters. Only node names have dots, because the dots group nodes, and a leading dot makes a node name relative to the file's prefix.

```text
@enum rooms.kitchen: fridge, sink
```

Use a single name: `@enum kitchen_spots: fridge, sink`.

### PIB1046

**A line is missing a part it needs,** such as the node name after `@jump`, `@call`, `==`, `@prefix` or `#was:`.

```text
@jump
```

Write the missing part: `@jump kitchen.leave`.

### PIB1047

**A number, or the number in a duration, can be at most 79228162514264337593543950335** (about 7.9 × 10^28). Digits after the decimal point past what fits, about 28 in all, are rounded rather than reported.

```text
@set $big = 99999999999999999999999999999
```

Write a smaller number.

### PIB1050

**A command's unnamed arguments come first, in order, then its named ones (`name=value`), then `wait` or `nowait`.** Named arguments can go in any order among themselves.

```text
@show at=left mira
```

Write `@show mira at=left`.

### PIB1051

**A call's `(` touches its name:** `has_item("key")`. With a space, I read the name and the brackets as two separate things. Where only a call makes sense, as in a condition or an `@set`, the space is reported. In a command's arguments, a space really does separate two arguments: `@give item ("key")` passes `item` and `"key"`.

```text
@if has_item ("key")
```

Remove the space: `@if has_item("key")`.

### PIB1052

**A line ID is a lowercase letter, then lowercase letters, digits and `_`.** IDs keep saves and translations attached to their lines, and they're used as file names for recorded voice lines, so they stay lowercase to work on every file system. You rarely type them: `pibbles ids` adds them.

```text
mira: Locked. #id:K7qp2x
```

Fix the ID by hand, or delete the tag and run `pibbles ids` to make a new one.

### PIB1053

**Some lines only take certain tags.** A node header only takes `#was:`, which records the node's old names, and `#was:` goes nowhere else. A variation block (`@sequence`, `@cycle`, `@once`) only takes `#id:`. A line takes one `#id:` at most, and a line that only changes a pose takes none, since a save never stops on it.

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
@if has_item("key"
```

Add the `)` where the brackets should close: `@if has_item("key")`.

### PIB1061

**Comparisons don't chain.** In some languages `$a < $b < $c` means "`$b` is between `$a` and `$c`", and in others it quietly compares `true` or `false` with `$c`. Pibbles asks you to say which you mean.

```text
@if 0 < $bravery < 3
```

Write each comparison out and join them with `and`: `@if 0 < $bravery and $bravery < 3`. If you really mean to compare the result of one comparison, put it in brackets: `@if ($a < $b) == $c`.

### PIB2001

**A line that starts with a name and a colon is spoken by that name,** so the name has to be an actor declared with `@actor`. A line that only happens to look like that, such as narration starting with `Note:`, needs its colon escaped, so it's never quietly shown as narration when a speaker was meant, or the other way round.

```text
Note: the door is locked.
```

If it's narration, escape the colon: `Note\: the door is locked.` If it's a speaker, fix the spelling, or declare the actor. The help suggests the closest actor, if one is close.

### PIB2002

**A pose is one of the poses its actor lists in `poses:`.** The help suggests the closest one, or lists them all.

```text
@actor mira
    name: Mira
    poses: neutral, smug

mira (smirk): I knew it.
```

Use one of the actor's poses, `mira (smug):`, or add the pose to the actor's `poses:`.

### PIB2003

**A pose is a single name,** so a parenthesis that holds anything else, before a colon, is reported rather than shown as narration. Directions for how a line is said, such as who it's said to, belong in a `//` comment above it.

```text
mira (to Rex): Fine.
```

Write the direction as a comment, `// To Rex.`, and the line as `mira: Fine.` If the line is narration, escape the colon: `Breakfast (served daily)\: eggs.`

### PIB2004

**A speaker's colon has a space after it.** Without one, the line is narration, which is rarely what's meant after an actor's name, so it's reported as a warning.

```text
mira:Hi.
```

Add the space, `mira: Hi.`, or escape the colon if the line really is narration: `mira\:Hi.` Names that aren't actors, as in `Time:3pm`, are never reported.

### PIB2010

**A command is declared with `@command` before the story uses it,** since the game has to carry out every command the story uses. The help suggests the closest command.

```text
@command shake_screen(strength: number = 1)

@shak
```

Fix the spelling, or declare the command.

### PIB2011

**An argument has the type of the parameter it's for.** Arguments without a name fill the parameters in order, and named ones (`at=left`) fill the parameter they name. Each is read against its parameter's type, so a bare name like `cellar` is looked up among that type's values. A number also works where a duration is expected, as seconds.

```text
@function has_item(id: string) -> bool

@if has_item(3)
```

Use a value of the parameter's type. Here `id` is text, so quote it: `has_item("3")`. The help lists what the type takes.

### PIB2012

**Only commands declared `inline` can go inside a line's text.** Loading a save made during a line shows the line again from the start, so its inline commands run again. `inline` marks the commands that are harmless to repeat, like a screen shake or a sound. Anything else runs once, on its own `@` line.

```text
@command give_item(name: string)

Here you go.{@give_item "key"}
```

Put the command on its own line, before or after the text: `@give_item "key"`. If repeating it really is harmless, declare it `inline`.

### PIB2013

**A call or command has at most as many arguments without a name as it has parameters.** The extra arguments are marked.

```text
@function has_item(id: string) -> bool

@if has_item("key", "lamp")
```

Remove the extra arguments. To check two items, call the function twice: `has_item("key") and has_item("lamp")`.

### PIB2014

**A named argument names one of the parameters** of the command or markup it's given to. The help suggests the closest parameter.

```text
@command show(who: actor, at: position = left)

@show mira att=left
```

Fix the name: `@show mira at=left`.

### PIB2015

**Every parameter without a default needs an argument,** in order or by name. A parameter with a default (`distance: number = 1`) can be left out.

```text
@function near(who: string, where: string, distance: number = 1) -> bool

@if near("mira")
```

Add the missing arguments in order: `near("mira", "door")`. For a command or markup, they can also be given by name: `@move mira to="door"`.

### PIB2016

**Each parameter gets one value.** An argument without a name fills the next parameter, so naming that parameter again gives it a second value.

```text
@command show(who: actor, at: position = left)

@show mira who=rex
```

Remove one of them: `@show rex`.

### PIB2017

**Markup is built in (`b`, `i`, `u`, `s` and `color`) or declared with `@markup`,** since the game styles every span the story uses. The help suggests the closest markup. Speed isn't built-in markup: unless the host declares its own `@markup speed`, the old `[speed 0.5]…[/speed]` is reported with the help "Did you mean `{speed}`?"

```text
@markup clue

That's [clu]the key[/clu].
```

Fix the spelling, `[clue]…[/clue]`, or declare the markup.

### PIB2018

**An icon is declared with `@icon`,** since the game draws every icon the story uses. The help suggests the closest icon.

```text
@icon interact

Press {icon interct}.
```

Fix the spelling, `{icon interact}`, or declare the icon.

### PIB2019

**A function is declared with `@function` before the story calls it,** since the game has to provide every function the story uses. `visits()` is the one function built into Pibbles.

```text
@function has_item(id: string) -> bool

@if has_iten("key")
```

Fix the spelling, or declare the function, so the game knows to provide it.

### PIB2020

**A node name in a `@jump`, a `@call`, `visits()` or a `node` argument names a node in the story,** in full or relative to the file's `@prefix`.

```text
@prefix kitchen

@if visits(.dor) > 0
```

Fix the spelling: `visits(.door)`. The help suggests the closest node, written relative if the name was.

### PIB2021

**A name starting with a dot is relative to the file's `@prefix`,** so in a file with `@prefix kitchen`, `.leave` means `kitchen.leave`. In a file with no prefix, there's nothing for it to be relative to.

```text
== .leave
mira: Onward!
```

Write the full name, `== kitchen.leave`, or add `@prefix kitchen` at the top of the file, above everything but comments.

### PIB2022

**Every node name is unique across the story, and so is every old name a node keeps in `#was:`.** The game starts nodes by name, and saves remember them, so one name can only ever mean one node. The second use is reported, with where the first one is.

```text
== kitchen.door
mira: Locked.

== kitchen.fridge #was:kitchen.door
mira: It hums.
```

`kitchen.door` names the first node, so it can't also be an old name of the second. Rename one of them. If a node was renamed and another node now has its old name, the old name has to go: keep `#was:` for names no node uses any more.

### PIB2023

**A prefix is a full node name, so a dot at its start does nothing.** `@prefix .kitchen` works just like `@prefix kitchen`: `.door` still means `kitchen.door`. It's reported so the file reads the way it works, but it's never an error.

```text
@prefix .kitchen
```

Remove the dot: `@prefix kitchen`.

### PIB2030

**A variable is declared with `@var` before the story uses it.** Variables hold what the story decides, and saves keep them, so every one is declared once, with its starting value.

```text
@var $has_key = false

@if $has_kye
```

Fix the spelling, or declare the variable.

### PIB2031

**A value has the type of what it's for:** a variable keeps the type it's declared with, a parameter's default has the parameter's type, and `@wait` takes a duration. A number works where a duration is expected, as seconds.

```text
@var $has_key = false

@set $has_key = 3
```

Use a value of the right type: `@set $has_key = true`. `+=` and `-=` keep the variable's type too, so `@set $count += 1s` is reported when `$count` is a number.

### PIB2032

**A condition is `true` or `false`.** There's no "truthy" value: `0`, empty text and every other value are neither, so the story says which question it's asking.

```text
@var $attempts = 0

@if $attempts
```

Write the comparison: `@if $attempts > 0`.

### PIB2033

**Each operator works on certain types,** and values never change type to fit one. `<` compares numbers or durations, `+` adds numbers or durations, or joins text, and `and`, `or` and `not` take `true` or `false`. The [operator types](language/reference.md#operator-types) list every combination.

```text
@var $name = "Sam"

@if $name < "Tom"
```

Text isn't ordered, so compare it with `==` or `!=`. To show a number in text, put it in the line as `{$count}` rather than adding it to text with `+`.

### PIB2034

**A bare name is a member of an enum, an actor or a node, so it only goes where one of those is expected:** an argument, a variable's value, or the other side of `==` or `!=`. Anywhere else, such as a whole condition or next to `+` or `and`, it has no type to be read against.

```text
@var $has_key = false

@if has_key
```

Variables start with `$`: `@if $has_key`. Text goes in quotes: `has_item("crowbar")`.

### PIB2036

**A bare name is one of the values of the type expected where it appears:** a member of the enum, a declared actor, or a node. The help suggests the closest value, or lists the enum's members.

```text
@enum position: left, right

@var $where: position = lfet
```

Fix the spelling, `left`, or add the member to the enum.

### PIB2037

**A bare name is read against the type on the other side of `==` or `!=`,** so two bare names have no type between them, and `left` could be a member of any enum.

```text
@if left == right
```

Compare a variable, a function or another value with the name: `@if $where == left`.

### PIB2038

**Text shows text, numbers and actors' names.** A number is formatted for the player's language, and an actor shows their display name. Other values, such as `true` and `false`, enum members, nodes and durations, aren't words a player should see, so text that depends on them chooses its words with `{if}`.

```text
@var $has_key = false

You have the key {$has_key}.
```

Write the words for each case: `You {if $has_key}have{else}don't have{/if} the key.` A function that returns text works too.

### PIB2039

**A speed, a pause or a wait has to be more than zero,** because a speed of zero would never show the next character, and a pause of nothing or less does nothing. This covers the value in `{speed x}`, `{w d}` and `@wait d`, when it's a constant: a literal, or arithmetic on literals, such as `0`, `0s`, `(-1)` or `(0.5s - 1s)`. A value that depends on a variable or a function call isn't checked here: if it isn't more than zero when the story runs, that speed, pause or wait is skipped with a warning.

```text
mira: Wait.{w 0} What?
```

Write a pause longer than zero, like `{w 0.5}`, or remove it. For a speed, write one above zero, like `{speed 0.5}`, or `{speed}` to return to the player's setting.

### PIB2045

**A tag is reserved (`#id`, `#was`) or declared with `@tag`,** so the game knows to read it. A `#` followed by a letter always starts a tag, so a `#` that's part of the text is escaped. The help suggests the closest tag.

```text
@tag thought

Hm. #thougth
```

Fix the spelling, `#thought`, or declare the tag. If the `#` is part of the text, write `\#`.

### PIB2046

**A tag declared on its own (`@tag thought`) is a flag, which takes no value. A tag declared with a type (`@tag box: string`) needs one,** after a colon. A value may be empty (`#box:`) only if the type ends in `?`.

```text
@tag thought, box: string

Hm. #thought:deep
Hi. #box
```

Write `#thought` on its own, and give `#box` a value: `#box:phone`.

### PIB2047

**The story always uses a node's current name.** Old names in `#was:` are for what's outside the story, such as saves and the game's code, so `@jump`, `@call`, `visits()` and `node` arguments use the current one, and old names never pile up in the script. An old name still reaches its node, so the story runs while a rename is half done, and `--warnaserror` keeps old names out of a release.

```text
@prefix kitchen

== .door #was:.front_door

@if visits(.front_door) > 0
```

Use the current name: `visits(.door)`.

### PIB2048

**A tag that takes an enum takes one of its members,** so the game only ever receives values it knows.

```text
@enum box_style: phone, letter
@tag box: box_style

Hi. #box:fone
```

Use one of the members, `#box:phone`, or add the value to the enum.

### PIB2060

**Each kind of name has one namespace, so each name is declared once per kind.** Two kinds can share a name, such as `@enum sfx` and `@command sfx`, because they're never used in the same place. The second declaration is reported, with where the first one is, and the first one is the one the story uses.

```text
@enum room: kitchen, cellar
@enum room: attic
```

Merge the two into one declaration (`@enum room: kitchen, cellar, attic`), or give one of them another name.

### PIB2061

**The poses of one actor, the members of one enum and the parameters of one declaration are all different.** The second of a pair is reported.

```text
@actor mira
    name: Mira
    poses: happy, sad, happy
```

Remove the second `happy`. If it was meant to be another pose, give it that name.

### PIB2062

**A name can't be a word Pibbles already uses where that name appears,** since the story couldn't tell the two apart. A command is written after `@`, so it can't be `if`; an actor can stand in a condition, so it can't be `true`. Each kind of name only avoids the words used in its own places, so `if` is fine as an icon or a pose. The [reserved words](language/reference.md#reserved-words) list them all, including a few kept for planned features.

```text
@command if()
```

Choose another name, such as `@command check_door()`.

### PIB2063

**The names you declare use only English letters (a to z, in either case), digits and `_`.** The game's code, file names and save files all handle those the same way everywhere. Display names and text are different: they can hold any character.

```text
@actor zoë
    name: Zoë
```

Replace the letter with one from a to z, as in `@actor zoe`, and keep `name: Zoë` for what the player sees.

### PIB2064

**A type is one of `bool`, `number`, `string`, `duration`, `node` and `actor`, or an enum declared with `@enum`.** Types go after a `:` in variables, parameters and tags, and after `->` in functions. The help suggests the closest type, if one is close.

```text
@enum position: left, right
@command show(at: postion)
```

Fix the spelling, `@command show(at: position)`, or declare the enum the type names.

### PIB2065

**Parameters with a default come after the ones without,** so the arguments without names always fill the required parameters, in order.

```text
@command shake(strength: number = 1, duration: duration)
```

Move the parameters that have defaults to the end: `@command shake(duration: duration, strength: number = 1)`.

### PIB2066

**A tag's value reaches the game as text,** so a tag that takes a value is either `string`, which accepts any value, or an enum, which accepts only its members. A `?` after the type also allows an empty value.

```text
@tag count: number
```

Use `@tag count: string`, or declare an enum with the values the tag can take and use that.

### PIB2067

**A variable that starts as a name has its type written out,** as in `@var $where: position = left`. A name like `left` could be a member of any enum, an actor or a node, and if the type came from the name, declaring a new name somewhere else could quietly change what the variable holds. When the name belongs to exactly one type, the help writes the declaration out with it.

```text
@enum position: left, right
@var $where = left
```

Write the type after the variable: `@var $where: position = left`.

### PIB2070

**Dividing by zero has no answer, so a story that does it gets 0 and a warning when it runs.** `/` and `%` both divide. This is reported when the right side is a constant that comes to zero: `0`, `0s`, or `(1 - 1)`. A divisor that depends on a variable or a function isn't checked here; if it's zero when the story runs, the result is 0 and the game receives a warning.

```text
@var $total = 10

== shop.till
@set $total = $total / 0
```

Divide by something that isn't zero. If you meant to guard against a zero that can happen, check it first: `@if $count > 0`.

### PIB3001

**A statement that always leaves its block, `@jump`, `@end` or `@return`, means nothing after it in that block runs.** An `@if` whose every branch leaves, `@else` included, counts too. Conditions are never worked out, so the check only goes by which statements are there: a choice or a variation never counts, since a choice is skipped when no option is available. Only the first line that never runs in a block is reported.

```text
@jump kitchen.leave
mira: Wait!
```

Remove the line, or move it above the `@jump`. If it should only run sometimes, put the `@jump` in an `@if`.

### PIB3002

**An option with no text shows up as an empty choice,** which is usually a forgotten line. It's information, not an error: the story runs, and the game decides what an empty option looks like, so a project that uses empty options on purpose can turn this off.

```text
->
    mira: Hm.
```

Write what the player picks after the `->`: `-> Think about it`.

### PIB3010

**Every line a save can stop on has an ID:** text lines that show text, options, `@call` lines and variation blocks. IDs keep saves, translations and recordings attached to their lines through every edit, and `pibbles ids` writes them, so this is a warning: the story still plays, and CI's `--warnaserror` keeps lines without one out of a release. A line that has a mistake Pibbles can't read past isn't checked, since its ID may be there.

```text
mira: Locked.
```

Run `pibbles ids`, which gives the line an ID at its end: `mira: Locked. #id:k7qp2x`.

### PIB3011

**Each line ID is used once in the whole story,** since saves and translations find a line by its ID. Copying a line copies its ID, so this usually means a pasted line. The later one is reported, with where the first one is.

```text
mira: Locked. #id:k7qp2x
mira: Still locked. #id:k7qp2x
```

Delete the ID from the copy and run `pibbles ids` to give it a new one. Keep the ID on the line that was there first, so saves and translations stay with it.

### PIB3012

**A line ID is never the same as a node's name or one of its old names,** so a name always means one thing. Generated IDs never clash; this comes from an ID written by hand.

```text
== intro
mira: Hi. #id:intro
```

Delete the ID and run `pibbles ids` to give the line a new one, or write a different ID by hand.

### PIB5001

**Lines nested more than three blocks deep are hard to follow,** and a choice deep inside other blocks gets lost. `@if`, options and variation blocks each count as a block. Only the first line of each block past the limit is reported. `pibbles_max_nesting` in `.editorconfig` sets the limit.

```text
-> Open the door
    @if $a
        @if $b
            -> Look closer
                This line is four blocks deep.
```

Move the inner part into a node of its own and `@call` it, or combine the conditions: `@if $a and $b`.

### PIB5002

**An option whose body runs longer than fifteen lines pushes the choice's other options out of sight,** so the choice can't be read as a whole. Blank lines and comments don't count. `pibbles_max_option_body` in `.editorconfig` sets the limit.

```text
-> Listen at the door
    (sixteen lines of story)
-> Leave
```

Move the body into a node of its own, and `@jump` to it from the option.

### PIB5003

**The same speaker saying the same text in three places is translated, and maybe recorded, three times.** A node with the line in it is translated and recorded once. Narration counts too. `pibbles_min_repeated_lines` in `.editorconfig` sets how many places it takes.

```text
== room.a
mira: This exact line appears in several nodes.

== room.b
mira: This exact line appears in several nodes.

== room.c
mira: This exact line appears in several nodes.
```

Put the line in a node of its own, such as `common.shared_line`, and `@call` it from each place.

### PIB5004

**A raw color used in two places usually means something,** such as a clue or a thought. A named markup says what it means, and the game can restyle it in one place. Colors that differ only in capital letters count as the same. `pibbles_min_repeated_colors` in `.editorconfig` sets how many places it takes.

```text
This line marks [color "#ff8800"]a phrase[/color] in orange.
This line marks [color "#ff8800"]another phrase[/color] the same way.
```

Declare a markup that says what the color means, such as `@markup clue`, ask whoever programs the game to style it, and use `[clue]…[/clue]` in each place.

### PIB5010

**A pose change nobody sees does nothing:** the actor already has the pose, or a pose-only line's pose changes again before any line shows. It's only reported when that's certain, with nothing but lines, `@set` and `@wait` between the two. A command, a call or a choice might change the pose, so after one nothing is known.

```text
mira (happy): This line sets the pose.
mira (happy): The pose on this line changes nothing.
```

Leave the pose out, `mira: …`, or remove the pose-only line nobody sees.

### PIB5011

**A pause that does nothing, or two that could be one:** `{w}` at the very end of a line, where the line already waits for a click; `{w}` just before `{p}`, which already waits; and two timed pauses side by side. A timed pause at the end of a line is fine, since it still delays auto mode.

```text
This line waits for a click that happens anyway.{w}
This line pauses{w 0.2}{w 0.3} in two steps.
```

Remove the extra `{w}`, or write one pause: `{w 0.5}`.

### PIB5012

**Markup that does nothing:** an empty span, a span inside one just like it, and two like spans side by side, which could be one. Spans are alike when they have the same name and the same arguments, so a `[color]` inside a different color isn't reported.

```text
This line is [b]split into[/b][b] two spans[/b].
```

Remove the empty or inner span, or join the two: `[b]one span[/b]`.

### PIB5013

**An `@if` or `{if}` whose branches are all the same makes no difference,** so the condition is only noise. It needs an `@else` or `{else}`, since without one the branch differs from showing nothing. Indentation, line IDs, blank lines and comments don't count as differences.

```text
@if $a
    This line appears either way.
@else
    This line appears either way.
```

Keep one copy of the branch, without the condition.

### PIB5014

**Reaching the end of a node returns,** so `@return` as its last line does nothing. An `@return` earlier in a node, such as inside an `@if`, is fine.

```text
== common.shared
This line is shared.
@return
```

Remove the `@return`.

### PIB5020

**A condition is already true or false,** so comparing it with `true` or `false` only makes it longer.

```text
@if $has_key == true
@if $has_key == false
```

Write `@if $has_key` and `@if not $has_key`.

### PIB5021

**`+=` and `-=` say "change this by"** more briefly than writing the variable twice. They work on numbers, durations and text.

```text
@set $attempts = $attempts + 1
```

Write `@set $attempts += 1`.

### PIB5030

**A line or option too long to read in one go** tires the player, and long options crowd the choice. Markup and pacing don't count, conditional text counts its longest branch, and a value shown with `{…}` counts nothing, since its length isn't known. A line counts its longest page, so `{p}` splits it. `pibbles_max_message_length` (300) and `pibbles_max_option_length` (80) in `.editorconfig` set the limits.

```text
-> This option is written as a full sentence that explains everything that will happen if it's picked
```

Split a long line into two lines, or into pages with `{p}`, which keeps one message and one voice clip. Shorten an option to what the player picks, and let the lines after it say the rest.

### PIB5031

**Names are written in `snake_case`:** nodes, actors, poses, variables, enums and their members, commands, markup, icons and functions. A display name such as `name: Mira` is what the player sees, so it's written any way. Only the place a name is declared is reported.

```text
@actor Mira
@var $HasKey = false
== Kitchen.FrontDoor
```

Rename it now, before saves, scenes or the game's code depend on it: `mira`, `$has_key`, `kitchen.front_door`. A name the game's code already uses needs care, since the code has to change with it.

### PIB5032

**Every block in a file indents by the same width,** so the structure reads the same everywhere. When `.editorconfig` sets `indent_size`, blocks indented with spaces follow it; otherwise the file's first block sets the width. A file indented with a different character than `indent_style` asks for is reported once. Mixing tabs and spaces in one file is a mistake of its own (PIB1001).

```text
@if $a
    This block is indented four spaces.
@if $b
  This block is indented two.
```

Indent the block like the rest of the file. Most editors can re-indent a selection for you.

### PIB5033

**Speakers are written `name (pose):`,** with one space before the pose, none inside the parentheses and none before the colon. Any spacing works, but one form keeps a script easy to scan.

```text
mira(happy): This line has no space before the pose.
mira : This line has a space before the colon.
```

Write `mira (happy):` and `mira:`.

### PIB5040

**A variable nothing in the story uses is dead weight.** Setting it counts as using it, since the game's code may read it. The game's vocabulary, such as commands, markup and actors, isn't checked: some of a toolbox going unused is normal.

```text
@var $old_flag = false
```

Remove the `@var`, or use the variable.

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
