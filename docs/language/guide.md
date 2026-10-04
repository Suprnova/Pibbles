# Writer's guide

This guide explains how to write stories in Pibbles, from a writer's point of view. It covers what you can do, not how it works inside. The technical version is the [language reference](reference.md), which wins if the two ever disagree.

Part one covers the basics, which are enough to write most scenes. Part two covers repeat visits, conditional wording, the file that lists the cast, and organizing your files. A cheat sheet at the end sums everything up.

---

# Part one: the basics

## Story files

Stories live in plain text files ending in `.pib`, inside the game's `story/` folder. You can name the files and organize folders however you like, and split a story into as many files as you want. Pibbles reads them all together as one story ([Organizing your files](#organizing-your-files)).

Any text editor works. Visual Studio Code with the Pibbles extension adds colors.

## The golden rule

**A line that doesn't start with a special symbol is story text.** Everything else is marked:

| A line starting with… | Means |
| --- | --- |
| `==` | The start of a new node (a scene or conversation) |
| `@` | An instruction: a condition, a jump, a stage direction |
| `->` | A choice the player can pick |
| `//` | A comment for other writers, which the player never sees |
| anything else | Dialogue or narration |

So you never have to worry about a normal sentence being mistaken for an instruction. Start a line with "If" or "Wait", and it's still just text.

## Nodes

A node is a named chunk of story, usually a scene, a conversation, or what a character says when you inspect something. It starts with `==` and a name, and runs until the next node or the end of the file.

```pib
== example.first_node
This line belongs to the first node.
So does this one.

== example.second_node
This line belongs to the second node.
```

- Names use lowercase letters, numbers and underscores. Dots let you group related nodes (`kitchen.fridge`, `kitchen.door`). A file can [set a prefix](#shorter-names-with-a-prefix) so you don't have to repeat the group name.
- Every name must be unique across the whole story.
- The game starts a node when something happens, such as the player walking into a room or clicking an object. When a node runs out of lines, the conversation ends.

## Dialogue and narration

Write the character's name, a colon, then what they say. Leave the name off for narration.

```pib
mira: This line is spoken by Mira.
rex: This line is spoken by Rex.
This line has no speaker, so it's narration.
```

- Each line is shown as one message.
- The name before the colon is the character's ID from the cast list ([Declarations](#declarations-the-cast-and-vocabulary)). The name the player sees can be different.
- If a narration line happens to start with a word and a colon (`Note: something`), Pibbles will think "Note" is a character and tell you so. Put a backslash before the colon to fix it: `Note\: something`.

## Poses

Add a pose in parentheses to change how a character looks. The pose stays until you change it again.

```pib
mira (happy): Mira's pose is now "happy".
mira: Mira is still happy on this line.
mira (sad): Now her pose is "sad".
mira (neutral):
```

The last line has no text. It changes Mira's pose without showing a message. A name and a colon with nothing at all after it, `mira:`, is an error. For a message box with only Mira's name in it, write `mira: {w}`. Each character's poses are listed in the cast list, and Pibbles tells you if you use one that doesn't exist.

The parentheses hold a single pose name, not a stage direction. `mira (to Rex): Fine.` is an error. To note how a line should be said, put a `//` comment above it.

## Choices

A group of `->` lines is a choice. Whatever is indented under an option happens when the player picks it.

```pib
This line appears before the choice.

-> First option
    This line only appears after picking the first option.
-> Second option
    This line only appears after picking the second option.
    So does this one.
-> Third option

This line appears after any option, once that option's lines are done.
```

- Indent with spaces or tabs, but don't mix both in one file.
- An option doesn't need anything under it. The third option above just carries on.
- After an option's lines finish, the story continues below the whole choice.
- Options stay available after they're picked, so if the story comes back to the same choice, the player can pick them again. The game knows which options were already picked, so it can grey them out. To remove an option after its first use, see [Option extras](#option-extras).

## Variables

Variables remember things: whether the player found a key, how many times they tried a door, what they named their character. Their names start with `$`.

```pib
@set $has_key = true
@set $attempts = 0
@set $attempts += 1
@set $attempts -= 1
@set $player_name = "Sam"
```

- A variable holds yes/no (`true` or `false`), a number, or text (in quotes).
- `+=` and `-=` add to or subtract from a number. `+=` also adds text to the end of a text variable: `@set $title += " the Brave"`.
- Every variable is listed in the cast list first, with its starting value. Pibbles catches typos like `$has_kye`.

To show a variable in a line, put it in curly braces:

```pib
The player's name is {$player_name}, and they've made {$attempts} attempts.
```

Text and number variables can be shown this way, and so can a variable holding a character, which shows the character's name. A yes/no variable can't be shown directly. Use [conditional wording](#conditional-wording) instead: `{if $has_key}you have the key{else}you don't{/if}`.

## Conditions

`@if` runs lines only when something is true. `@elif` ("else if") tries another condition, and `@else` catches everything else. Indent the lines each one controls.

```pib
@if $has_key:
    This line only appears if $has_key is true.
@elif $attempts > 3:
    This line appears if there's no key but more than three attempts.
@else:
    This line appears otherwise.
```

Comparisons you can use:

| Write | Means |
| --- | --- |
| `$a == $b` | is equal to |
| `$a != $b` | is not equal to |
| `>`, `<`, `>=`, `<=` | greater than, less than, or equal |
| `and`, `or`, `not` | combine conditions: `$has_key and not $door_open` |

## Moving between nodes

| Instruction | What it does |
| --- | --- |
| `@jump example.other_node` | Goes to another node and continues there. |
| `@call example.shared_node` | Runs another node, then comes back here and continues. Good for lines several scenes share. |
| `@return` | Inside a node that was called, goes back early. |
| `@end` | Ends the conversation right away. |
| `@wait 1s` | Pauses for a moment with no text. Write times like `2s` or `500ms`. |

```pib
== example.start
This line is shown first.
@call example.shared
This line is shown after the shared node finishes.
@jump example.finish

== example.shared
This line can be reused by any node that calls it.

== example.finish
This line is shown last.
```

## Formatting text

Square brackets style part of a line. Open with `[name]` and close with `[/name]`.

```pib
This word is [b]bold[/b], this one is [i]italic[/i], this one is [u]underlined[/u], and this one is [s]struck through[/s].
This text is [color "#ff8800"]orange[/color].
This phrase is marked as a [clue]clue[/clue].
This text [wave]wobbles[/wave].
```

- `b`, `i`, `u`, `s` and `color` always work.
- Other styles like `clue` and `wave` are made for the game and listed in the cast list. **Prefer named styles like `[clue]` to raw colors.** Changing how every clue looks is then a one-line change for the game.
- Styles can be combined, but must close in the reverse order they opened: `[b][i]text[/i][/b]`.
- A style must open and close within the same line.

## Pacing

Curly braces mark a point in the line. These ones control timing as the text types out:

```pib
This line pauses briefly here,{w 0.5} then keeps going.
This line waits for the player to click here,{w} then keeps going.
This line fills one box.{p}This part starts on a fresh page after a click.
This line has a line break here{br}and continues on the next line.
This part types at normal speed, [speed 0.3]this part types slowly,[/speed] and [speed 3]this part fast.[/speed]
```

| Write | Effect |
| --- | --- |
| `{w 0.5}` | Pause for that many seconds |
| `{w}` | Wait for a click, then continue on the same page |
| `{p}` | Wait for a click, clear the box, continue |
| `{br}` | Start a new line |
| `[speed 2]…[/speed]` | Type faster (above 1) or slower (below 1), relative to the player's speed setting |

## Stage directions and game events

Lines starting with `@` followed by a command name tell the game to do something: show a character, play a sound, move the camera, start a puzzle.

```pib
@show mira left
@show rex right
@sfx door_rattle
@shake_screen 0.5
@move mira center
@hide rex
```

The available commands, and what each one needs, are listed in the cast list. Pibbles tells you if a name is misspelled or a detail is missing.

**Commands inside a line** happen at that exact point as the text types out:

```pib
This sound plays right here,{@sfx thud} in the middle of the line.
```

Only some commands can be used inside a line: the ones marked `inline` in the cast list, usually sounds, animations and camera moves. Everything else, like giving the player an item, goes on its own `@` line. If a player loads a save made in the middle of a line, that line plays again, in-line commands included. Keeping game-changing commands out of lines means they can never run twice. If you try to put one inside a line, Pibbles tells you.

**Waiting for a command to finish:** some commands, like a camera move, make the story wait until they're done. Others run while the story carries on. Add `nowait` or `wait` to change that for one use:

```pib
@move mira offscreen nowait
This line appears while Mira is still moving.
```

## Special characters

Some characters mean something to Pibbles: `[ ] { } # \` and, in some places, `:` and `@`. To write one literally, put a backslash in front of it. A `#` only needs one when a letter follows it, and so does an `@` in option text:

```pib
This line shows \[square brackets\] and \{curly braces\}.
This line ends with a hashtag \#literally.
This line shows a backslash: \\
-> Email me \@home
```

## Comments

```pib
// A comment for other writers. The player never sees it.
This line is story text. // This is NOT a comment. It's part of the line.
```

In dialogue, narration and options, a comment must be on its own line, so a line that contains a web address or `//` still reads correctly. A comment line can sit at any indentation.

## Line IDs

Lines get a short ID at the end:

```pib
mira: This line has an ID. #id:k7qp2x
```

Variation blocks get one too, after the colon (`@once: #id:b8k2qd`), so the story remembers how often each block has run. You don't type these. A tool adds them, and they keep saves attached to the right line. Leave them alone when editing a line, even if you rewrite its text completely, or move it to another file. The editor fades them out so they don't get in the way. If you copy and paste a line, Pibbles notices the duplicate ID. If you delete a line, its ID goes with it.

---

# Part two: more features

## Different lines on repeat visits

When the player inspects the same thing again, it's nicer to say something new. Variation blocks pick different lines each time the story reaches them. Each option starts with `- `, and can have more indented lines under it.

```pib
@sequence:
    - This line is shown the first time.
    - This line is shown the second time.
    - This line is shown the third time and every time after.

@cycle:
    - This line is shown the first time.
    - This line is shown the second time.
    - Then it starts over from the first line.
        @sfx sigh
        This line belongs to the same option.

@once:
    This line is only shown the first time.
    @set $saw_this = true
```

| Block | Behavior |
| --- | --- |
| `@sequence:` | Goes through the options in order, then stays on the last one |
| `@cycle:` | Goes through the options in order, then loops back to the first |
| `@once:` | Runs only the first time, then is skipped |

## Conditional wording

To change a few words instead of a whole line, put a condition inside the line:

```pib
This line says {if $has_key}you have the key{else}you don't have the key{/if}.
This line says {if $attempts > 3}many{elif $attempts > 0}some{else}no{/if} attempts were made.
```

This keeps the line as one message, which is easier to translate and voice than two nearly identical lines.

## Option extras

After an option's text you can add:

```pib
-> This option is always available
-> This option needs the key  @if $has_key
-> This option disappears after it's picked once  @once
-> This option has both  @if $has_key @once
```

- **`@if`** makes an option unavailable unless the condition is true. The game decides whether an unavailable option is hidden or shown greyed out.
- **`@once`** removes the option for good after it's picked.
- If no options are available, the choice is skipped and the story continues below it.

Option text can use styles, variables, icons and conditional wording, but not pauses or commands. Those go in the lines under the option.

## Visit counts

Pibbles counts how many times each node has been reached. `visits(node name)` gives that number:

```pib
@if visits(example.room) == 1:
    This line only appears on the first visit.
@elif visits(example.other_room) > 0:
    This line appears if the other room has been visited at least once.
```

## Tags

Tags are labels at the end of a line or option. Players never see them. They pass extra information to the game.

```pib
mira: This line might be shown as a thought bubble. #thought
-> This option might be shown greyed out when unavailable  @if $has_key #show_disabled
mira: This line might be shown in a phone-message box. #box:phone
```

Which tags mean something depends on the game, so the available tags are listed in the cast list. Pibbles tells you if you use one that isn't listed, so a typo like `#thougth` never slips through. Tags always come last on the line: once a tag starts, only more tags can follow it. To write a hashtag as part of the text, escape it: `\#winning`.

Some tags are on or off, like `#thought`. Others need a value after a colon, like `#box:phone`. The cast list says which is which, and Pibbles tells you if a value is missing or shouldn't be there.

## Icons

Icons put a small picture inside the text, such as a controller button. The game shows the right picture for whatever the player is using: keyboard, Xbox pad, and so on.

```pib
Press {icon interact} to inspect things, and {icon inventory} to open your bag.
```

## Asking the game questions

The game can answer questions about things Pibbles doesn't track itself, like the player's inventory. They're used like `visits`:

```pib
@if has_item("crowbar"):
    This line appears if the player is carrying the crowbar.
This line mentions an item by its display name: {item_name("crowbar")}.
```

Write the brackets right up against the name: `has_item("crowbar")`, not `has_item ("crowbar")`. The available questions are listed in the cast list.

## Declarations: the cast and vocabulary

One or more files (usually a single file like `defs.pib`) list everything the story is allowed to use. Writers and programmers keep them together, because the game has to support everything listed. Declarations go above the first node in a file.

```pib-standalone
// Characters and their poses
@actor mira:
    name: Mira
    poses: neutral, happy, sad

// Fixed lists of names, used by commands and variables
@enum position: left, center, right
@enum sound: thud, door_rattle, sigh

// Variables and their starting values
@var $has_key = false
@var $attempts = 0
@var $player_name = "Sam"

// Commands, with what each one needs
@command show(who: actor, at: position = center)
@command sfx(name: sound) inline
@command give_item(id: string)
@command move(who: actor, to: position) waits

// Text styles, icons, tags and questions for the game
@markup clue
@icon interact, inventory
@tag thought, show_disabled, box: string
@function has_item(id: string) -> bool
@function item_name(id: string) -> string
```

To read a command declaration:

- `show(who: actor, at: position = center)` means `@show` needs a character and optionally a position, which defaults to center. So `@show mira` and `@show mira left` both work.
- `inline` means the command can also be used inside a line, like `{@sfx thud}`. Commands without it can only go on their own `@` line.
- `waits` means the story waits for the command to finish unless you write `nowait`.

To read a tag declaration:

- `thought` on its own is an on-or-off tag: write `#thought`, never `#thought:something`.
- `box: string` needs a value: write `#box:phone`.
- `box: string?` also allows an empty value: `#box:` means "no box style", which is different from leaving the tag off.

If something you need isn't listed, ask for it to be added. Don't work around it in the story.

## Organizing your files

Every `.pib` file in the story folder belongs to one big story. How you split it into files is up to you. A file per room, per chapter or per character all work the same way.

```text
story/
  defs.pib              characters, commands, shared variables
  common.pib            lines reused from many places
  rooms/
    kitchen.pib
    cellar.pib
```

- **Everything is shared.** A node in one file can jump to or call a node in any other file, and every file can use every character, variable and command, wherever it was declared. There's nothing to "include" or "import".
- **Files are just for you.** Moving a node to another file, or renaming a file or folder, changes nothing for the game or for other files, as long as the node's full name stays the same ([prefixes](#shorter-names-with-a-prefix)).
- **Declarations can sit next to the story that uses them.** A room's file can declare that room's variables at the top, above its first node. They still work everywhere.
- **Names are shared too,** so every node name must be unique across all files. Starting node names with the room or chapter (`kitchen.`, `cellar.`) keeps them apart. If two files use the same name, Pibbles points out both places.

```pib
// rooms/kitchen.pib
// A variable only the kitchen uses, declared above the first node.
@var $drawer_open = false

== kitchen.drawer
@set $drawer_open = true
This node continues in a node from another file.
@jump cellar.stairs
```

## Shorter names with a prefix

When most nodes in a file start with the same group name, put a `@prefix` line at the top of the file. Then a name that starts with a dot gets the prefix filled in:

```pib
// rooms/kitchen.pib
@prefix kitchen

== .drawer
This node's full name is kitchen.drawer.
@jump .window

== .window
This node's full name is kitchen.window.
@jump cellar.stairs

== common.stuck
This node's full name is common.stuck, because it doesn't start with a dot.
```

- **Only names that start with a dot get the prefix.** Every other name is already complete, so `@jump cellar.stairs` still reaches the cellar, and `kitchen.window` works as well as `.window`.
- **The game and error messages always use the full name,** like `kitchen.drawer`.
- **The prefix is part of the name.** Changing the prefix, or moving a dot-named node into a file with a different prefix, renames the node. To move a node without renaming it, write its full name in the header. Otherwise, see [Renaming a node](#renaming-a-node).

## Renaming a node

The game refers to nodes by name: a door in a scene starts `kitchen.door`, and saved games remember which nodes the player has visited. Renaming a node would break those, so keep the old name in a `#was:` tag on the header:

```pib
== kitchen.front_door #was:kitchen.door
This node used to be called kitchen.door.
```

- **The game and old saves still find the node** under its old name. Your own story uses the new name everywhere. If a `@jump` or `@call` still uses the old one, Pibbles tells you the new name.
- **A node can have several old names** if it's renamed more than once: `#was:kitchen.door #was:kitchen.entrance`.
- **You only need it once something uses the name.** A node nobody has played or connected to the game yet can be renamed freely.

## Checking your work

- **`pibbles check`** checks the whole story at once and lists every problem with its file and line: unknown characters or poses, misspelled variables or commands, missing node names, unclosed styles.
- **`pibbles play`** plays the story as text in a terminal, with numbered choices and effects shown in place, so you can test a branch without launching the game.

---

# Cheat sheet

| Write | Meaning |
| --- | --- |
| `== area.name` | Start a node |
| `@prefix area` then `== .name` | At the top of a file: `.name` means `area.name` |
| `== area.name #was:area.old_name` | Rename a node without breaking the game or old saves |
| `mira: text` | Mira says a line |
| `mira (happy): text` | Change Mira's pose, then she speaks |
| `mira (happy):` | Change Mira's pose silently |
| `text` | Narration |
| `-> text` | Choice option |
| `-> text  @if $x` | Option only available if `$x` is true |
| `-> text  @once` | Option disappears after it's picked |
| `@if …:` / `@elif …:` / `@else:` | Conditions |
| `@set $x = value` / `+=` / `-=` | Change a variable |
| `@jump node` | Go to another node |
| `@call node` / `@return` | Run another node and come back |
| `@end` | End the conversation |
| `@wait 1s` | Pause without text |
| `@sequence:` `@cycle:` `@once:` | Vary lines on repeat visits |
| `@command args` | Stage direction or game event |
| `[b]…[/b]` `[i]` `[u]` `[s]` `[color "#hex"]` | Formatting |
| `[speed 0.5]…[/speed]` | Slower or faster typing |
| `{$x}` | Show a variable's value |
| `{w 0.5}` / `{w}` | Timed pause / wait for click |
| `{p}` / `{br}` | New page / new line |
| `{@command}` | Effect at this point in the line (`inline` commands only) |
| `{icon name}` | Inline icon |
| `{if …}…{else}…{/if}` | Conditional wording |
| `#tag` | Label for the game (listed with `@tag`) |
| `// comment` | Note to other writers |
| `\` | Write the next special character as-is |
