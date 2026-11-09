# Cheat sheet

What you write in a story, on one page. The [writer's guide](guide.md) explains the language, and the [language reference](reference.md) is the full technical version.

**What you write keeps working.** Once a release freezes the language, a story that `pibbles check` passes keeps passing and plays the same in every release after it, apart from the few kinds of change listed under [Compatibility](reference.md#compatibility). Each release's notes list every change you could notice.

## Story files

Stories are `.pib` files in the game's `story/` folder. All the files make one story: any node can jump to a node in another file, and every file can use everything the cast list declares.

**The golden rule:** a line that doesn't start with `==`, `@`, `->` or `//` is story text. A line starting with "If" or "Wait" is still just text.

## Nodes and names

| Write | Meaning |
| --- | --- |
| `== area.name` | Start a node. It runs until the next `==` or the end of the file. When its lines run out, the conversation ends, or goes back after an `@call` |
| `@prefix area` then `== .name` | At the top of a file: `.name` means `area.name`, and `$.name` means `$area.name` |
| `== area.name #was:area.old_name` | Rename a node without breaking the game or old saves |

Write names in lowercase, with letters from any language, numbers and `_`, not starting with a number. Dots group them (`kitchen.door`). Every node name and variable name is unique across the whole story.

## Lines

| Write | Meaning |
| --- | --- |
| `mira: text` | Mira says a line |
| `text` | Narration: a line with no speaker |
| `mira (happy): text` | Change Mira's pose, then she speaks. The pose stays until you change it |
| `mira (happy, shake): text` | Set a value in each of two channels, in any order, then she speaks |
| `mira (happy):` | Change Mira's pose without showing a line |
| `(shake): text` | Narration that sets a value |
| `mira: {w}` | A box with only Mira's name in it |
| `Note\: text` | Narration that starts with a word and a colon |

Type markers such as `@`, `->`, `#`, `[ ]`, `{ }` and `//` in half-width (direct input). A speaker's colon, parentheses and commas can be full-width: `mira（happy）：`. Indent with spaces or tabs, not both in one file. Under a Japanese input method, press Tab: the space bar types a full-width space, which doesn't indent.

## Choices

| Write | Meaning |
| --- | --- |
| `-> text` | An option. The indented lines under it run when it's picked, then the story carries on below the whole choice |
| `-> text  @if $x` | Only available while `$x` is true |
| `-> text  @once` | Gone for good after it's picked |
| `-> text  @if $x @once` | Both |

Option text can use styles, `{$x}`, icons, conditional wording and `{br}`. It can't hold `{pause}`, `{w}`, `{page}`, `{speed}` or commands.

## Variables and conditions

| Write | Meaning |
| --- | --- |
| `@set $x = value` | Change a variable. Values are `true` or `false`, numbers, `"text in quotes"`, or names like `mira` |
| `@set $x += 1` / `-= 1` | Add or subtract. `+=` also adds text to the end of a text variable |
| `@if …` / `@elif …` / `@else` | Conditions. Indent the lines each one controls |
| `==` `!=` `>` `<` `>=` `<=` | Equal, not equal, greater, less, greater or equal, less or equal |
| `and` `or` `not` | Combine conditions: `$has_key and not $door_open` |
| `visits(area.name)` | How many times a node has been reached. Inside the node, the current visit already counts |
| `has_item("crowbar")` | Ask the game a question (listed with `@function`). No space before the `(` |

## Moving between nodes

| Write | Meaning |
| --- | --- |
| `@jump area.name` | Go to another node and continue there |
| `@call area.name` / `@return` | Run another node, then come back / come back early |
| `@end` | End the conversation |
| `@pause 1s` | Pause without text. Times look like `2s`, `500ms` or `0.5` (seconds) |

## Repeat visits

| Write | Meaning |
| --- | --- |
| `@sequence` | Runs the next option each time the story reaches it, then stays on the last |
| `@cycle` | Runs the next option each time the story reaches it, then starts over |
| `@once` | Runs the indented lines below only the first time |
| `- line` | One option of a `@sequence` or `@cycle`, with more indented lines under it if you like |
| `-` on its own | An option made of the indented lines below, so it can start with `@if` |

## Stage directions

| Write | Meaning |
| --- | --- |
| `@show mira left` | Tell the game to do something. The commands are made for the game and listed with `@command` |
| `@move mira center nowait` | Carry on without waiting for the command to finish. `wait` makes the story wait |
| `{@sfx thud}` | Run a command at this point in the line (only commands marked `inline`) |

## Inside a line

| Write | Meaning |
| --- | --- |
| `[b]…[/b]` `[i]` `[u]` `[s]` `[color "#ff8800"]` | Bold, italic, underlined, struck through, colored. Close on the same line, in reverse order: `[b][i]…[/i][/b]` |
| `[clue]…[/clue]` | A style made for the game (listed with `@markup`). Prefer these to raw colors |
| `{$x}` | Show a variable's value: text, a number or a character's name. Other variables can't be shown: use `{if}` |
| `{item_name("crowbar")}` | Show the game's answer to a question, when it's text or a number |
| `{if …}…{elif …}…{else}…{/if}` | Conditional wording |
| `{pause 0.5}` | Pause for that long: `0.5` or `500ms` |
| `{w}` | Wait for a click, then continue on the same page |
| `{page}` | Wait for a click, clear the box, continue |
| `{br}` | Start a new line |
| `{speed 2}` / `{speed}` | Type faster (above 1) or slower (below 1) from here / back to normal. Every line starts at normal speed |
| `{icon jump}` | An icon from the game (listed with `@icon`), such as the button to press. Name it after the action (`jump`), not the button (`a_button`): the game shows the right button or key for whatever the player is using |

## Tags, comments and line IDs

| Write | Meaning |
| --- | --- |
| `#thought` / `#box:phone` | A tag for the game, which players never see (listed with `@tag`). Tags always come last on the line |
| `#id:k7qp2x` | A line ID. `pibbles ids` adds them; leave them alone, even when you rewrite the line |
| `// comment` | A note to other writers, on its own line or at the end of one. `///` is an error, while `////` is fine |
| `\` | Write the next special character as-is: `\[`, `\{`, `\\`, `https:\//example.com`. A `#` needs one only before a letter (`\#winning`), and so does an `@` in option text (`\@home`) |

## The cast list

Declarations go above a file's first node. A `@var` can also go between nodes, right above the one that uses it, and still works everywhere.

| Write | Meaning |
| --- | --- |
| `@channel pose: neutral, happy` | A channel: something a line sets for its speaker, like a pose or an effect (`@channel effect: still, shake`). These are the values every character and narration can use. Each begins on the first one. With no values (`@channel pose`), each character lists its own |
| `@actor mira` | A character. Indented below it: `name: Mira`, `pose += smug` (adds), `pose: neutral, grumpy` (replaces), `no effect` (drops a channel) |
| `@narration` | What narration has, with the same indented lines as a character |
| `@enum position: left, center, right` | A fixed list of names |
| `@var $has_key = false` | A variable and its starting value. One that starts as a name says what it holds: `@var $who: actor = mira`, or `@var $where: position = left` for a name from an `@enum` |
| `@command show(who: actor, at: position = center)` | A command and what it needs. `= center` is the default. `inline` after it allows it inside a line, and `waits` makes the story wait for it |
| `@markup clue` | A style |
| `@icon jump, interact` | Icons |
| `@tag thought, box: string` | `#thought` is on or off, and `#box` needs a value, any text (`string`). `box: string?` also allows an empty `#box:` |
| `@function has_item(id: string) -> bool` | A question the game answers. This one takes text and answers yes or no (`bool`) |

## Checking your work

| Run | What it does |
| --- | --- |
| `pibbles init` | Start a new project, with a short example story |
| `pibbles check` | List every problem in the story, with its file and line |
| `pibbles check --style` | Also show style hints: suggestions, not problems |
| `pibbles explain PIB2004` | What a message means and how to fix it |
| `pibbles ids` | Add line IDs wherever they're missing |
| `pibbles play --start area.name` | Play the story as text, starting at a node. `--record bug.transcript` saves the playthrough as a transcript. Move it into a `transcripts` folder beside your `story` folder to keep it as a test |
| `pibbles test` | Play every transcript in the `transcripts` folder again, and show any whose story has changed. If every change is what you meant, `pibbles test --update` saves them |

To silence a style hint you mean to keep, put `// pibbles-ignore` and its code (`// pibbles-ignore PIB5003`) at the end of its line or on the line above it. On its own line above a file's first node, it covers the whole file.
