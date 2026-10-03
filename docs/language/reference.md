# Language reference

This reference is the authoritative specification of the `.pib` language: its syntax, what each construct means, and what the analyzer checks. When code and this reference disagree, the code is wrong or the change isn't finished. Writers learn the same language from the [writer's guide](guide.md), and [`samples/kitchen`](../../samples/kitchen/) shows it in context.

The [language design](design.md) explains why the language is shaped this way, and holds the [grammar extensions](design.md#grammar-extensions): features that are designed but wait until a need for them comes up. New features only ever add to the grammar, and v1 already reserves every word, tag and line marker they use, so adding one never changes what a valid v1 story means. The [known exceptions](#extension-compatibility) are line show counts for inline variations and the stretch-goal plurals.

## At a glance

```pib
== kitchen.door
@if $door_open:
    @jump kitchen.leave

mira (worried): Locked.{w} Of course it's locked.

-> Rattle the handle
    @sfx door_rattle
    @jolt mira
    mira (angry): [shout]Open up![/shout]
    @set $bravery += 1
-> Use the key  @if $has_key
    @sfx key_turn
    mira (happy): Got it!
    @jump kitchen.leave
-> Leave it
    @end

rex: {if $bravery > 2}Maybe stop hitting it?{else}Well, that didn't work.{/if}
```

## Files and structure

- Source files use the `.pib` extension and are UTF-8. Any line-ending style is accepted.
- A story is every `.pib` file under the story folder, compiled together. Names are global across files, and file names and folders carry no meaning to the language.
- A file has two regions: **declarations** first, then **nodes**. The node region starts at the first node header (`==`). A file may contain only declarations or only nodes. An optional [`@prefix`](#prefixes) line comes before both.
- An optional `pibbles.json` in the project root holds project settings: version, source locale, and `story`, the story folder (`story` by default). Without it, defaults apply.
- The project root is the folder the tools are pointed at: the CLI's `[root]` argument (the current directory by default). Diagnostic severities live in `.editorconfig` ([tooling design](../tooling.md#configuration)).

## Line classification

The parser classifies each line by how it starts, before looking at the rest of the line.

| A line starting with… | Is a… |
| --- | --- |
| `==` | Node header |
| `///` | Reserved for notes, which arrive with localization ([roadmap](../roadmap.md#feature-tiers)). An error in v1. |
| `//` | Comment |
| `@` | Statement or declaration |
| `->` | Choice option |
| `- ` (inside a variation block) | Variation alternative |
| anything else | Text line: dialogue or narration |

**The text-first rule:** a line that doesn't start with one of these markers is always text. So *"If you say so."* is narration, not a broken `if` statement.

## Lexical basics

- **Indentation** defines blocks. A file must indent with spaces or with tabs, not both. A block opens after a statement or declaration ending in `:`, which tags and a comment may follow (`@if`, `@elif`, `@else`, `@sequence`, `@cycle`, `@once`, `@actor`), or after a `->` option or `-` alternative.
- **Blank lines** are ignored.
- **Comments:** `//` starts a comment on a line of its own, or after the content of a statement, declaration or node header. Text lines can't have trailing comments, because `//` shows up in normal prose and URLs.
- **Identifiers:** letters, digits and `_`, not starting with a digit. They are case-sensitive. The convention is `snake_case`. Any Unicode letter is recognized as a letter, but a declared name uses ASCII letters only. So `Café: open late` is read as an unknown speaker rather than as narration, and allowing non-ASCII names later can't change what a valid story means.
- **Node names:** identifiers joined by dots (`kitchen.door`). A name starting with a dot (`.door`) is relative to the file's [prefix](#prefixes).
- **Variables:** `$` followed by an identifier (`$has_key`).
- **Escapes:** in text, a backslash makes the next punctuation character literal: `\[`, `\{`, `\#`, `\:`, `\\`, and so on.
- **Tags:** `#name` or `#name:value` at the end of a text line, option, `@call`, variation block opener or node header. A node header takes only reserved tags, since the host never reads it. A tag name must start with a letter, so *"my #1 fan"* is text. The value runs until whitespace, and may be empty (`#name:`). `#id` holds [line IDs](../localization.md#line-ids) and `#was` [node aliases](#nodes). `#migrates`, `#draft`, `#voice` and `#unvoiced` are reserved for [extensions](#reserved-words). Every other tag must be declared with [`@tag`](#declarations), which says whether it takes a value, and is passed to the host unchanged.
- **Line IDs:** an `#id` value is a lowercase letter followed by lowercase letters, digits and `_`. Generated IDs are a letter and five letters or digits (`k7qp2x`). Starting with a letter keeps every ID a valid identifier, which the [migration extension](design.md#migrations) relies on. Lowercase keeps IDs distinct as file names on case-insensitive file systems, since voice clips are keyed by ID. A line ID is never the same as a node name or alias, so a name that could be either always means one thing.
- **Where IDs go:** `#id` goes on text lines that show text, on options, on `@call` and on variation block openers (`@once: #id:b8k2qd`), at most once each. Lines, options and calls are what a save can point at, and a block's ID keys its [entry count](#variations). A pose-only line (`mira (sad):`) takes none: it shows nothing and completes at once, so a save never waits on it.

## Declarations

Declarations make up the contract between the story and the host ([boundaries](../boundaries.md#the-contract-goes-both-ways)). They are global, order doesn't matter, and they may only appear before a file's first node.

```pib-standalone
@actor mira:
    name: Mira
    poses: neutral, happy, sad, smug, worried, angry

@enum position: left, center, right, offscreen
@enum room: kitchen, cellar

@var $has_key = false
@var $current_room: room = kitchen

@command show(who: actor, at: position = center)
@command move(who: actor, to: position) waits
@command jolt(who: actor) inline
@command enter_room(which: room, on_exit: node)

@markup clue
@markup wave(amplitude: number = 1, frequency: number = 5)

@icon interact, inventory

@tag thought, show_disabled, box: string

@function has_item(id: string) -> bool
```

| Declaration | Meaning |
| --- | --- |
| `@actor id:` | A character. `name:` is the display name, and defaults to the ID. `poses:` lists valid poses; the first one is the default. |
| `@enum name: a, b, c` | A closed set of values. Use it for positions, rooms, sound IDs, anything that should be typo-proof. |
| `@var $name [: type] = literal` | A variable, saved with the game. The type comes from the initial value unless written out. A name as the initial value (an enum member, actor or node) needs the type written out. |
| `@command name(params) [inline] [waits]` | A host instruction. `inline` allows it inside a line's text ([below](#commands)). `waits` means the story waits for it to finish by default. |
| `@markup name[(params)]` | A span tag. |
| `@icon a, b, …` | Inline icon names. |
| `@tag name [: type[?]], …` | Tags the host understands. A bare name is a flag: `#thought`, never with a value. `name: type` requires a value (`#box:phone`), which is checked against the type: `string` accepts any value, and an enum accepts its members. `type?` also allows an empty value (`#box:`), which reaches the host as null. |
| `@function name(params) -> type` | A host function usable in expressions. It must have no side effects. The CLI and tests call stubs instead. |

**Parameters:** `name: type [= default]`. Required parameters come before optional ones.

**Variables hold what the story decides. The game keeps everything else.** Inventory, solved puzzles and anything that outlives a save slot live in the game, reached through functions and commands.

**Types:** `bool`, `number` (a 64-bit float), `string`, `duration`, `node`, `actor`, and any declared enum.

**The prelude:** Pibbles ships a small built-in declaration file with `@markup b`, `i`, `u`, `s` and `color(value: string)`. The host can't redeclare these names. `speed` is a built-in markup with core semantics, not a prelude declaration.

Declared names share one namespace per kind, and each kind avoids the [reserved words](#reserved-words) that could be read in its positions.

## Nodes

```pib
== kitchen.fridge #was:kitchen.icebox
```

A node header gives a globally unique name, full or [relative](#prefixes), and optional `#was:` aliases. The node's body is every line up to the next header or the end of the file, starting at column 0.

- The host starts dialogues at nodes by name. Nodes jump to and call each other.
- A node's **visit count** goes up each time it's entered, whether by the host, `@jump` or `@call`. `visits(kitchen.fridge)` reads it.
- Reaching the end of a node returns to the caller after an `@call`. Otherwise it ends the dialogue.
- **`#was:old.name`** records a former name after a rename. Node names held outside the story still find the node through it: names in the game's code and scenes, saved visit counts, and continuations a save holds, like an `on_exit=` argument. A node can list several, full or relative. Aliases are unique across the story like names are.
- **The story itself always uses current names.** A `@jump`, `@call`, `visits()` or `node` argument that names an alias is an error that suggests the current name, so aliases never pile up in the source.

## Prefixes

A file that keeps its nodes under one group name can declare it once:

```pib
@prefix kitchen

== .door
@if $door_open:
    @jump .exit
@call common.stuck

== .exit
mira: Onward!

== common.stuck
mira (sad): Nope.
```

- **`@prefix name`** comes before everything else in the file except comments, at most once. The name is a node name, so it may contain dots (`@prefix rooms.kitchen`).
- **A name starting with a dot is relative:** in the file above, `.door` means `kitchen.door`. Relative names work anywhere a node name does: headers, `@jump`, `@call`, `visits()`, `node` arguments (`on_exit=.leave`) and `#was:`.
- **A name without a leading dot is always the full name,** in every file.
- **There is no lookup order.** A relative name never falls back to a global one, and a full name is never tried under the prefix. Adding a node can't change what an existing reference points to.
- **A relative name in a file without `@prefix` is an error.**
- **Only the source uses relative names.** The host API, saves, diagnostics and the CLI always use full names.
- **The prefix is part of each relative node's name.** Changing a file's prefix renames its relative-named nodes, and so does moving one into a file with a different prefix. Writing the full name in the moved header keeps it. Once the game's code, scenes or saves use the old name, a rename needs a `#was:` alias like any other.

The prefix is written in the file, not derived from its name or folder, so renaming or moving files never renames nodes.

## Statements

### Text lines

```pib
mira: Hello there.
mira (smug): I knew you'd come.
The kitchen smells of old coffee.
mira (sad):
```

- **`speaker:`** where `speaker` is a declared actor, followed by a space or the end of the line. Spaces between the parts don't matter: `mira (happy):`, `mira(happy):` and `mira :` are all speaker prefixes, and a style hint tidies them to `mira (happy):`. Anything else is narration.
- **Lines that almost look like a speaker are reported, never silently shown as narration.** If a narration line happens to look like a speaker (`Note: the door is locked.`), the analyzer reports an unknown actor and suggests escaping: `Note\: …`. A parenthesis that holds more than one name (`mira (to Rex): Fine.`) is an error, since a pose is a single name. A declared actor followed directly by a colon and text (`mira:Hi`) is a warning that suggests adding a space.
- **`speaker (pose):`** sets the actor's pose, then shows the line. The pose stays until it's changed again.
- **`speaker (pose):` with no text** changes the pose without showing a line.
- Each line is shown as one message, and its text is inline-markup text ([below](#inline-text)). Leading and trailing whitespace is trimmed.

### Choices

```pib
-> Rattle the handle
    mira: Nope.
-> Use the key  @if $has_key #show_disabled
    @jump kitchen.leave
-> Knock politely  @once
    rex: Really?
-> Leave it
    @end
```

- Consecutive `->` options at the same indentation form one choice. Each option's body is the more-indented block below it, and may be empty.
- **Option text** supports markup, interpolation, icons and conditionals. It does not support commands or pauses: those belong in the body.
- **Options are sticky by default:** an option stays available after the player picks it. `@once` makes an option disappear after it's picked.
- **Modifiers** come after the text: `@if expr` makes the option available only while `expr` is true, and `@once` removes the option for good after it's chosen once. Tags go last.
- **When a choice is reached:** options removed by `@once` are left out. Every other option goes to the host with its tags, an `IsAvailable` flag, and a `WasChosen` flag. The host decides whether an unavailable option is hidden or shown greyed out, for example with a `#show_disabled` tag of its own.
- **Availability and option text are evaluated when the choice is reached,** and again when a save restores it, so a restored choice reflects the current state.
- **If no option is available,** the choice is skipped.
- **Picking an option records it as chosen** (for `WasChosen` and `@once`) before its body runs, so a save made inside the body already counts it. The host may only pick an available option.
- **After an option's body finishes** (without `@jump` or `@end`), flow continues at the first line after the whole choice.
- The chosen option's text is **not** repeated as a line.

### Conditionals

```pib
@if $door_open:
    mira: It's open!
@elif has_item("crowbar"):
    mira: Time for plan B.
@else:
    mira: Still locked.
```

### Variables

```pib
@set $has_key = true
@set $bravery += 1
@set $bravery -= 1
```

The value's type must match the variable's declared type.

### Flow

| Statement | Effect |
| --- | --- |
| `@jump node` | Continue at `node`. The call stack is unchanged. |
| `@call node` | Run `node`, then come back here. A call carries an `#id` like a line does, since a save can be waiting inside the called node. |
| `@return` | Return from the current `@call`. At the top level it acts as `@end`. |
| `@end` | End the dialogue and clear the call stack. |
| `@wait duration` | Pause the story for a moment without showing text (`@wait 1s`). |

### Variations

```pib
@sequence:
    - mira: It's a fridge.
    - mira (smug): Still a fridge.
    - mira: I refuse to look at this fridge again.

@cycle:
    - rex: Hm.
    - rex: Huh.

@once: #id:b8k2qd
    mira: Whoa, a secret panel!
    @set $found_panel = true
```

An alternative starts with `- ` followed by a statement. Lines indented more deeply below it continue the same alternative.

Each block has an **entry count** `n` in the state: how many times execution has entered it, keyed by the block's `#id`. On entry, the runner reads `n`, picks what to run from it, stores `n + 1`, then runs the pick. With `count` alternatives:

| Block | Runs |
| --- | --- |
| `@sequence:` | Alternative `min(n, count − 1)`: the next one each time, then stays on the last |
| `@cycle:` | Alternative `n mod count`: the next one each time, starting over after the last |
| `@once:` | Its body if `n` is 0; after that it's skipped |

- **The count goes up on entry, before the pick runs.** An alternative that ends in `@jump` or `@end` still counts. Restoring a save inside an alternative resumes there without entering the block again.
- **A block that isn't reached doesn't count.** A block in an `@if` branch that isn't taken keeps its count.
- **A block has one count wherever it's reached from,** including from several `@call` sites.
- **Edits keep counts meaningful.** Adding or removing alternatives, or changing a block's kind, still gives every saved count a defined result, since each kind derives its pick from the count. The `#id` keeps the count attached to the block when blocks are inserted, reordered or moved to another node.

### Commands

```pib
@show mira left
@move mira offscreen nowait
@enter_room kitchen on_exit=kitchen.leave
@shake_screen ($bravery * 0.5)
```

- `@name` followed by positional arguments, then named arguments (`name=value`), then optionally `wait` or `nowait`.
- **Positional arguments** are simple expressions: literals, variables, bare identifiers, dotted names (including relative node names) and function calls. Anything with operators goes in parentheses.
- **Bare names** are read against the parameter's type: enum members, actors and node names ([bare names](#bare-names)). Strings are always quoted.
- **Inline use is opt-in.** Only commands declared `inline` can appear inside text as `{@name …}`. Every other command can only be used on its own `@` line, where it runs exactly once. Loading a save made during a line shows that line again from the start, and its inline commands fire again ([runtime design](../runtime.md#saving-mid-dialogue)). So `inline` is for presentational commands that are harmless to repeat, and the analyzer rejects any other command in text.
- **Wait semantics:** a command declared with `waits` makes the story wait until the host reports it done. `nowait` overrides that for one call. `wait` does the opposite for a command without `waits`.
- The analyzer checks the command's name, argument count, types, named arguments and required parameters.

## Expressions

- **Literals:** `true`/`false`, numbers (`3`, `0.5`), strings (`"text"`, with `\"` and `\\` escapes), durations (`0.5s`, `300ms`, or a plain number meaning seconds where a `duration` is expected).
- **References:** `$variable`, `function(args)`, and [bare names](#bare-names) for enum members, actors and nodes.
- **Operators,** from lowest to highest precedence: `or`; `and`; `not`; `== !=`; `< <= > >=`; `+ -`; `* / %`; unary `-`; then parentheses. `+` also joins strings.
- **Built-in functions:** `visits(node) -> number`.
- **Static typing:** every expression has a type the analyzer knows. Types are never converted implicitly, except that a number can stand in for a duration ([operator types](#operator-types)).

### Operator types

| Operator | Operands | Result |
| --- | --- | --- |
| `not` | bool | bool |
| `and`, `or` | bool, bool | bool |
| `==`, `!=` | Two values of the same type, any type | bool |
| `<`, `<=`, `>`, `>=` | Two numbers, or two durations | bool |
| `+` | Two numbers, two durations, or two strings | The operands' type |
| `-` | Two numbers, or two durations | The operands' type |
| `*` | Two numbers; or a duration and a number, either way round | number; duration |
| `/` | number ÷ number; duration ÷ number; duration ÷ duration | number; duration; number |
| `%` | Two numbers | number |
| unary `-` | A number or a duration | The operand's type |

- **A number is read as seconds wherever a duration is expected:** a duration parameter or variable, and the other side of `+`, `-`, `==`, `!=`, `<`, `<=`, `>` and `>=` when one side is a duration. So `0.5s + 1` is 1.5 seconds, and `$delay > 2` compares against 2 seconds. In `*` and `/` a number is a factor, never a time.
- **Conditions are bool.** `@if`, `@elif`, option `@if` and `{if}` take a bool, with no truthiness: `@if $attempts:` is an error that suggests `$attempts > 0`.
- **`$x += v` means `$x = $x + v`,** and likewise for `-=`. The result must have `$x`'s type, so `+=` works on numbers, durations and strings.
- **Strings compare by exact characters,** case-sensitive and independent of the locale: `"Sam" == "sam"` is false.

Strings and enums aren't ordered, a number never becomes a string (text shows numbers through interpolation, `{$n}`), and values of different types never compare equal. Each of these is an error, so adding one later can't change a valid story.

### Bare names

Enum members, actors and node names are written as bare names (`left`, `mira`, `kitchen.door`). A bare name is always read against the type expected where it appears, and is looked up only among that type's values:

| Expected type | A bare name is |
| --- | --- |
| An enum | One of that enum's members |
| `actor` | A declared actor |
| `node` | A node name, full or [relative](#prefixes) |
| `string`, `number`, `bool`, `duration` | Never valid. Strings are quoted, and variables start with `$`, so the analyzer suggests `"crowbar"` or `$has_key`. |

The expected type comes from:

- **A parameter,** for command, markup and function arguments, and `node` for `visits()`.
- **The variable,** for `@set`, and for `@var` when its type is written out. A `@var` whose initial value is a name must write its type (`@var $where: position = left`), so declaring a new enum member, actor or node can never change an existing variable's type.
- **The other operand,** for `==` and `!=`. Comparing two bare names (`left == right`) is an error.

A bare name anywhere else, such as a whole condition (`@if has_key:`) or an operand of `+`, `<` or `and`, is an error. A dotted name is always a node name.

## Inline text

Everything after the speaker prefix of a text line, and the text of an option, is **inline text**:

- **`[square brackets]` are ranges** (markup spans). They style or pace a run of text.
- **`{curly braces}` are points or computed values.** They mark a spot in the text or produce text.

### Markup spans

```pib
mira: That's [b]not[/b] a normal key. It's [clue]the master key[/clue].
rex: [wave amplitude=2]Spooooky.[/wave]
mira: [speed 0.3]Very... slowly...[/speed] there.
```

- The syntax is `[name args]…[/name]`. Arguments work like command arguments.
- Spans must nest properly (`[b][i]…[/i][/b]`), must close on the same line, and can't cross a conditional boundary.
- `[speed x]` multiplies reveal speed by `x`, relative to the player's setting.

### Points and values

| Syntax | Meaning |
| --- | --- |
| `{$var}`, `{fn(args)}` | Interpolates the value. A `string` shows as written, a `number` is formatted with the current locale's culture, and an `actor` shows its display name. Other types (`bool`, enums, `node`, `duration`) can't be interpolated: they aren't player-facing text. Use conditional text (`{if $has_key}…{/if}`), or a host function that returns a string. |
| `{@command args}` | Runs a command declared `inline` at this point in the reveal. `{@jolt mira wait}` holds the reveal until the host finishes. |
| `{w}` | Waits for player input, then continues on the same page. |
| `{w 0.5}` | Pauses the reveal for this long (a duration). |
| `{p}` | Page break: waits for input, clears the box, continues. |
| `{br}` | Line break. |
| `{icon name}` | A declared icon, which counts as one character. |

### Conditional text

```pib
rex: You {if $bravery > 2}actually hit it{elif $bravery > 0}barely touched it{else}haven't even tried{/if}.
```

The branches can contain any inline text, including markup and points. Only the chosen branch's points fire.

### Where inline elements are allowed

| Element | Text line | Option text |
| --- | --- | --- |
| Markup spans, interpolation, icons, conditionals | Yes | Yes |
| `{br}` | Yes | Yes |
| Commands, `{w}`, `{p}` | Yes | No |

## Runtime semantics summary

- **Dialogues are pull-based.** The host starts a node and repeatedly asks for the next step: a line, a choice, a command, a pose change, a wait, or the end. See [runtime design](../runtime.md#runtime-model).
- **A statement-level command is a step.** The host carries it out and asks for the next step, either right away or after it finishes, depending on the wait flag.
- **An inline command is a marker** at a character position in the line. The reveal helper fires it when the reveal reaches that position. If the player skips the reveal, the command markers it skipped past still fire, in order. Timing markers (pauses, speed) are dropped.
- **Skipping never changes the outcome.** Every `@set`, command and effect still happens, in order. Only timing is dropped.
- **Visit counts, block entry counts and chosen options** live in the story state and are saved with it.

## Reserved words

A word is reserved only where it could be read two ways. Each kind of declared name appears in its own positions, so each kind avoids only the words that can appear there too. Words marked *(ext)* are used by [extensions](design.md#grammar-extensions): v1 gives them no meaning, but rejects them in the same positions, so adding an extension never changes what a valid v1 story means.

### Word groups

| Group | Words |
| --- | --- |
| Statement and declaration keywords (after `@`) | `prefix`, `actor`, `enum`, `var`, `command`, `markup`, `icon`, `tag`, `function`, `if`, `elif`, `else`, `set`, `jump`, `call`, `return`, `end`, `wait`, `sequence`, `cycle`, `once`; *(ext)* `term`, `resume`, `shuffle` |
| Brace keywords (after `{`) | `w`, `p`, `br`, `icon`, `if`, `elif`, `else`; *(ext)* `auto`, `sequence`, `cycle`, `shuffle`, `once` |
| Value words | `true`, `false`, `and`, `or`, `not` |
| Argument words | `wait`, `nowait`; *(ext)* `speaker` |
| Built-in functions | `visits`; *(ext)* `random` |
| Built-in types | `bool`, `number`, `string`, `duration`, `node`, `actor` |
| Built-in markup | `speed`, and the prelude's `b`, `i`, `u`, `s`, `color` |

### What each kind of name can't be

| Name kind | Appears | Can't be |
| --- | --- | --- |
| Command | After `@` and `{@` | A statement or declaration keyword |
| Markup | After `[` | Built-in markup |
| Function | In expressions and `{name(…)}` | A brace keyword, value word or built-in function |
| Enum | In types | A built-in type |
| Enum member, single-segment node name | Bare in arguments and expressions | A value word or argument word |
| Actor | Bare in arguments and expressions, and *(ext)* `{actor (pose)}` | A value word, argument word or brace keyword |
| Parameter | In `name=value` | `wait` or `nowait` |
| *(ext)* Term | `{name}` | A brace keyword or function |
| *(ext)* Persona | Inside `@actor` | `default`, the name of the base presentation |
| Pose, icon, tag, variable, segment of a dotted node name | Only in their own positions | Nothing reserved |

A clash is reported at the declaration (or node header). Declared names share one namespace per kind: two kinds may use the same name, such as `@enum sfx` and `@command sfx`, because their positions never overlap.

**Contextual words** are keywords only where the syntax lists them, and are never reserved: `name`, `poses`, `inline`, `waits`, and *(ext)* `required` and `persona`.

**Reserved tags:** `#id` and `#was`; *(ext)* `#migrates`, `#draft`, and `#voice` and `#unvoiced` ([voice](../localization.md#recorded-lines)). A tag can't be declared with one of these names.

**Reserved line marker:** *(ext)* `///`.

## Appendix: grammar

The grammar has four layers, and each one reads only what the layer below it produces:

1. **Lines.** The source splits into lines. Each line is classified by its leading marker, and indentation becomes INDENT and DEDENT tokens.
2. **Tokens.** A regular grammar for each lexer mode.
3. **Syntax.** A context-free grammar over the tokens. It's LL(1): every decision the recursive-descent parser makes depends only on the next token.
4. **Checks.** Rules the grammar leaves to the parser and analyzer, either because a context-free grammar can't express them or because a check gives a better diagnostic.

Two parts of the language aren't context-free: indentation, handled in layer 1, and matching a `[/name]` to its `[name]`, handled in layer 4.

A production marked `/* Qn */` depends on an [open question](#open-questions). Each question is decided when its production is implemented, and this section is updated then. A decision prefers rejecting a form over accepting it with a fallback meaning, such as "treat it as text": a rejected form can gain a meaning later without changing any valid story, and an accepted one can't. [Definition order](#definition-order) lists every production in the order it gets formally defined.

### Notation

W3C EBNF, the notation of the XML specification. `::=` defines a production, `|` separates alternatives, and `?`, `*` and `+` mean optional, zero or more, and one or more. `"…"` is a literal, `[a-z]` and `[^…]` are character classes, `#xN` is a code point, and `A - B` matches A but not B. Tokens are `UPPER_CASE` and productions are `snake_case`. `[ ]` and `{ }` are Pibbles syntax, so they never appear as notation.

### Lines

```ebnf
source          ::= #xFEFF? line (newline line)*                          /* Q1 */
newline         ::= #xD #xA | #xA | #xD
line            ::= indent content
indent          ::= ws*
ws              ::= #x20 | #x9
content         ::= [^#xA#xD]*
```

Each line is classified by how its `content` starts, checked in this order:

| `content` starts with | Class |
| --- | --- |
| nothing | BLANK |
| `///` | NOTE (Q2) |
| `//` | COMMENT |
| `==` | HEADER |
| `@` | AT |
| `->` | OPTION |
| `-` followed by `ws` | DASH |
| anything else | TEXT |

The parser reads two classes by context, which the classifier doesn't know:

- **DASH** is an alternative only as a direct child of a variation block. Its content after `- ` is classified again, as a line of its own. Anywhere else, a DASH line is a TEXT line, by the text-first rule.
- **TEXT** inside an `@actor` block is an actor property.

v1 reports every NOTE line as an error.

**Indentation** turns widths into tokens:

1. A stack of widths starts as `[0]`. A line's width is the number of characters in its `indent`. A file indents with spaces or with tabs, never both, so each character counts as one.
2. BLANK lines are skipped (Q3 for COMMENT lines).
3. A line wider than the top of the stack pushes its width and emits INDENT. A narrower line pops widths and emits a DEDENT for each until the top is no wider than the line. If the top then isn't equal to the line's width, the indentation is inconsistent: that's an error, and the line joins the innermost block it fits in.
4. Every line ends with EOL. At the end of the file, each width left above 0 emits a DEDENT.

INDENT and DEDENT come from widths alone. An INDENT where the syntax allows no block is an error.

### Tokens

The lexer has three modes:

- **Code mode** reads HEADER and AT lines after their marker, actor properties, option modifiers, and everything inside `[…]` and `{…}`. Whitespace between tokens is insignificant, except as Q16 decides. The longest match wins.
- **Inline mode** reads text: a TEXT line after its speaker, option text, and the text around spans and points.
- **Raw mode** reads an actor's `name:` value.

Code mode:

```ebnf
letter          ::= /* any character in Unicode category L */
mark            ::= /* any character in Unicode category M */
digit           ::= [0-9]
ident           ::= (letter | "_") (letter | mark | digit | "_")*
NAME            ::= "."? ident ("." ident)*
VARIABLE        ::= "$" ident
NUMBER          ::= digit+ ("." digit+)?                                  /* Q5 */
DURATION        ::= NUMBER ("ms" | "s")              /* not followed by a letter, digit or "_" */
STRING          ::= '"' ([^"#x5C#xA#xD] | #x5C ["#x5C])* '"'
TAG             ::= "#" letter (letter | digit | "_")* (":" [^#x20#x9#xA#xD]*)?     /* Q6 */
COMMENT         ::= "//" [^#xA#xD]*                                       /* Q7 */
AT_WORD         ::= "@" ident
PUNCT           ::= "(" | ")" | "," | ":" | "?" | "=" | "+=" | "-=" | "->"
                  | "==" | "!=" | "<" | "<=" | ">" | ">=" | "+" | "-" | "*" | "/" | "%"
```

**Keywords.** Where the syntax lists a [reserved word](#reserved-words) as a literal, a NAME spelled that way is that keyword. An AT_WORD is a statement or declaration keyword when its word is one, and a command otherwise. Declarations can't use a reserved word where it would be read as the keyword, so no valid story can mean anything else by it. [Contextual words](#reserved-words) (`name`, `poses`, `inline`, `waits`) are keywords only where the syntax lists them.

Inline mode:

```ebnf
SPEAKER         ::= ident (ws* pose)? ws* ":"      /* followed by ws or the end of the line */
pose            ::= "(" ws* ident ws* ")"
TEXT            ::= text_char+
text_char       ::= [^[{#x5C#xA#xD]              /* except where a tag starts (Q9) or, in option text, a modifier (Q10) */
ESCAPE          ::= #x5C escapable                                        /* Q11 */
SPAN_OPEN       ::= "["
SPAN_CLOSE      ::= "[" ws* "/"
BRACE           ::= "{"
BRACE_IF        ::= "{" ws* "if"
BRACE_ELIF      ::= "{" ws* "elif"
BRACE_ELSE      ::= "{" ws* "else"
BRACE_CLOSE     ::= "{" ws* "/"
```

- SPEAKER is only tried at the start of a TEXT line. If it doesn't match, the whole line is inline text.
- The lexer looks ahead at characters before it picks a token, which is separate from the parser's one-token lookahead. A `#` starts a tag only when the character after it is a letter, so `my #1 fan` needs no escape. `\#` is only needed when `#` followed by a letter should be text.
- A `[` or `{` fuses with a `/` or keyword after it into one token, and a fused keyword can't be followed by a letter, digit or `_`. This keeps every inline decision to one token: `{elif` ends the branch before it, while a plain `{` starts a point.
- After a bracket or brace token, the lexer reads code mode up to the matching `]` or `}`, then returns to inline mode.
- `]` and `}` on their own are text.

Raw mode:

```ebnf
RAW             ::= [^#xA#xD]*                                            /* Q7, Q12 */
```

### Structure

```ebnf
file            ::= prefix_line? declaration* node*
prefix_line     ::= "@prefix" NAME EOL
node            ::= header_line statement*
header_line     ::= "==" NAME TAG* EOL
block           ::= INDENT statement+ DEDENT
```

### Declarations

```ebnf
declaration     ::= actor_decl | enum_decl | var_decl | command_decl
                  | markup_decl | icon_decl | tag_decl | function_decl
actor_decl      ::= "@actor" NAME ":" EOL INDENT actor_prop+ DEDENT
actor_prop      ::= name_prop | poses_prop
name_prop       ::= "name" ":" RAW EOL                                    /* Q12 */
poses_prop      ::= "poses" ":" name_list EOL
enum_decl       ::= "@enum" NAME ":" name_list EOL
icon_decl       ::= "@icon" name_list EOL
tag_decl        ::= "@tag" tag_entry ("," tag_entry)* EOL
tag_entry       ::= NAME (":" type "?"?)?
name_list       ::= NAME ("," NAME)*
var_decl        ::= "@var" VARIABLE (":" type)? "=" constant EOL
type            ::= NAME                             /* a single identifier */
constant        ::= literal | NAME                                        /* Q13 */
literal         ::= NUMBER | DURATION | STRING | "true" | "false"
command_decl    ::= "@command" NAME "(" params? ")" command_flag* EOL
command_flag    ::= "inline" | "waits"
markup_decl     ::= "@markup" NAME ("(" params? ")")? EOL
function_decl   ::= "@function" NAME "(" params? ")" "->" type EOL
params          ::= param ("," param)*
param           ::= NAME ":" type ("=" constant)?
```

### Statements

```ebnf
statement       ::= text_line | choice | if_stmt | set_stmt | flow_stmt
                  | wait_stmt | variation | command_stmt
text_line       ::= SPEAKER? inline_text TAG* EOL                         /* Q9, Q14 */
choice          ::= option+                          /* greedy: consecutive options are one choice */
option          ::= "->" inline_text option_modifier* TAG* EOL block?     /* Q9, Q10 */
option_modifier ::= "@if" expr | "@once"
if_stmt         ::= "@if" expr ":" EOL block elif_clause* else_clause?
elif_clause     ::= "@elif" expr ":" EOL block
else_clause     ::= "@else" ":" EOL block
set_stmt        ::= "@set" VARIABLE assign_op expr EOL
assign_op       ::= "=" | "+=" | "-="
flow_stmt       ::= "@jump" NAME EOL | "@call" NAME TAG* EOL | "@return" EOL | "@end" EOL
wait_stmt       ::= "@wait" expr EOL
variation       ::= ("@sequence" | "@cycle") ":" TAG* EOL INDENT alternative+ DEDENT
                  | "@once" ":" TAG* EOL block
alternative     ::= "-" alternative_line block?                           /* Q15 */
alternative_line ::= /* Q15 */
command_stmt    ::= AT_WORD arg* wait_flag? EOL
arg             ::= NAME arg_tail? | VARIABLE | literal | "(" expr ")"
arg_tail        ::= "=" value | call_args                                 /* Q16 */
value           ::= NAME call_args? | VARIABLE | literal | "(" expr ")"   /* Q16 */
call_args       ::= "(" (expr ("," expr)*)? ")"
wait_flag       ::= "wait" | "nowait"
```

- Every statement starts with a different token: a TEXT line, `->`, a distinct AT_WORD keyword, or a command. `@elif` and `@else` start no statement, so one that doesn't follow an `@if` block is an error.
- `arg` folds positional and named arguments together, so the parser never has to look past a NAME for an `=`. Their order is a [check](#checks).

### Inline text

```ebnf
inline_text     ::= inline_item*
inline_item     ::= TEXT | ESCAPE | span | point | cond
span            ::= SPAN_OPEN NAME arg* "]" inline_text SPAN_CLOSE NAME "]"
point           ::= BRACE point_body "}"
point_body      ::= VARIABLE
                  | NAME call_args                                        /* Q16 */
                  | AT_WORD arg* wait_flag?
                  | "w" arg?
                  | "p"
                  | "br"
                  | "icon" NAME
cond            ::= BRACE_IF expr "}" inline_text
                    (BRACE_ELIF expr "}" inline_text)*
                    (BRACE_ELSE "}" inline_text)?
                    BRACE_CLOSE "if" "}"
```

### Expressions

```ebnf
expr            ::= or_expr
or_expr         ::= and_expr ("or" and_expr)*
and_expr        ::= not_expr ("and" not_expr)*
not_expr        ::= "not" not_expr | eq_expr
eq_expr         ::= rel_expr (("==" | "!=") rel_expr)*                    /* Q17 */
rel_expr        ::= add_expr (("<" | "<=" | ">" | ">=") add_expr)*        /* Q17 */
add_expr        ::= mul_expr (("+" | "-") mul_expr)*
mul_expr        ::= unary (("*" | "/" | "%") unary)*
unary           ::= "-" unary | primary
primary         ::= literal | VARIABLE | NAME call_args? | "(" expr ")"
```

`or`, `and`, `+`, `-`, `*`, `/` and `%` are left-associative. An expression ends at the first token that can't continue it, such as `:`, `}`, a tag, `@once` or the end of the line.

### Checks

- A span closes with the name it opened with, innermost first.
- Arguments come positional first, then named, then `wait` or `nowait`. Spans take no `wait` or `nowait`.
- A name being declared is a single identifier, except in `@prefix` and node headers. A leading dot marks a relative node name, and is valid only where a node is expected.
- Declarations come before a file's first node, and `@prefix` comes before everything but comments.
- A variation block holds only alternatives.
- A speaker is a declared actor. Otherwise the analyzer suggests escaping the colon.
- A text line that starts with a name, then a parenthesis that isn't a single identifier, then a colon (`mira (to Rex):`) is an error. A text line that starts with a declared actor directly followed by a colon and a character other than whitespace (`mira:Hi`) is a warning. Both suggest escaping the colon if the line is narration.
- A tag is reserved or declared with `@tag`. An unknown tag suggests the closest declared name, or escaping the `#` if it's meant as text.
- A flag tag has no `:`. A value tag has one, followed by a value of its type. The value may be empty only if the type is marked `?`. A tag type is `string` or a declared enum.
- `#id` has the [line ID](#lexical-basics) shape, at most once, only on a text line that shows text, an option, `@call` or a variation block opener. A block opener takes no other tag.
- `#was` names a node, only on a node header. A node header takes no other tag in v1.
- Node names, node aliases and line IDs are all distinct from one another.
- Option text can't contain commands, `{w}` or `{p}` ([where inline elements are allowed](#where-inline-elements-are-allowed)).
- A declared name isn't a [reserved word](#reserved-words) for its kind, and uses ASCII letters only.

### Open questions

| Q | Production | Question | Options |
| --- | --- | --- | --- |
| 1 | `source` | Is a byte-order mark allowed? Some Windows editors write one. | Allowed and skipped; an error |
| 2 | NOTE class | Which lines are notes? "Starts with `///`" also catches `////` comment banners. | Any line starting with `///`; `///` followed by `ws` or the end of the line, with longer runs of slashes being comments |
| 3 | Indentation | Do COMMENT lines take part? | Skipped like BLANK lines, so a comment can sit at any indentation; indented like any other line |
| 5 | `NUMBER` | Which number forms are allowed? | Also `.5` (safe, since a NAME can't start with a dot then a digit); also `1.`; exponents; digit separators |
| 6 | `TAG` | Which characters can a tag name hold? | Identifier characters only; also `-` |
| 7 | `COMMENT`, `RAW` | Which lines can end in a `//` comment? Text lines can't, but option lines and actor `name:` values also end in text. | By mode: `//` is a comment only in code mode, so an option can have one after its modifiers or tags but never right after its text; by line: no line with a text or raw part has comments |
| 9 | `text_char`, `text_line`, `option` | Where do trailing tags start? `I'm #winning today` | A `#` followed by a letter always starts a tag, and text after a tag is an error (escape with `\#`); only the run of tags at the end of the line is tags, and any `#` before it is text |
| 10 | `text_char`, `option` | Where does option text end? `-> Email me @ home` | At whitespace followed by `@if` or `@once`; at any `@` (escape with `\@`); at the run of modifiers and tags at the end of the line |
| 11 | `ESCAPE` | Which characters can follow `\`? What about a `\` at the end of a line? A letter or digit after `\` must be an error ([extension compatibility](#extension-compatibility)). | Punctuation only, with anything else an error; anything but letters and digits; an unknown escape is literal text |
| 12 | `name_prop`, `RAW` | What can a display name hold? `[`, `{` and `\` in it must be errors ([extension compatibility](#extension-compatibility)). | Plain text, trimmed; inline text with markup, which makes it inline mode instead of raw |
| 13 | `constant`, `arg` | Can a constant or positional argument be negative (`@var $x = -1`, `@foo -1`)? This belongs in the syntax: a negative NUMBER token would break `$a-1`. | `"-"? NUMBER` in `constant`, with arguments needing parentheses; `"-"? NUMBER` in both |
| 14 | `text_line` | What does `mira:`, with neither pose nor text, do? | An error; shows an empty line; nothing |
| 15 | `alternative` | Which statements can follow `- `? `- @if $x:` opens a block that would also be the alternative's continuation. | Single-line statements only (text lines, `@set`, flow, `@wait` and commands); any statement, with a block opener's block serving as the continuation |
| 16 | `arg_tail`, `value`, `point_body` | Is `NAME (…)` a call, or a NAME followed by another argument? `@foo bar (x)` needs a second token past `bar` to decide. The mid-line pose extension depends on the answer for points ([language design](design.md#pose-changes-partway-through-a-line)). | Adjacency: `bar(x)` is a call, `bar (x)` is two arguments; a call in a positional argument needs parentheses, as in `(bar(x))`, so a `(` after a NAME always starts a new argument |
| 17 | `eq_expr`, `rel_expr` | What do `a == b == c` and `a < b < c` mean? | Left-associative, so `(a == b) == c` type-checks and `a < b < c` doesn't; non-associative, so both are syntax errors; chained, as in Python |

### Definition order

Characters and tokens are defined first, from the bottom up, because every later production depends on which tokens exist. The syntax is then defined from the top down, so every production is reachable from `file` and the dispatch on each line is complete. Expressions come last: they're self-contained, and follow directly from the precedence list.

| # | Productions | Questions |
| --- | --- | --- |
| 1 | `source`, `newline`, `line`, `indent`, `ws`, `content` | Q1 |
| 2 | Line classification | Q2 |
| 3 | Indentation: INDENT, DEDENT, EOL | Q3 |
| 4 | `letter`, `mark`, `digit`, `ident` | |
| 5 | `NAME`, `VARIABLE` | |
| 6 | `NUMBER`, `DURATION`, `STRING` | Q5 |
| 7 | `TAG` | Q6 |
| 8 | `COMMENT` | Q7 |
| 9 | `AT_WORD`, `PUNCT`, keywords | |
| 10 | `SPEAKER`, `pose` | |
| 11 | `TEXT`, `text_char` | Q9, Q10 |
| 12 | `ESCAPE` | Q11 |
| 13 | `SPAN_OPEN`, `SPAN_CLOSE`, `BRACE`, `BRACE_IF`, `BRACE_ELIF`, `BRACE_ELSE`, `BRACE_CLOSE` | |
| 14 | `RAW` | Q7, Q12 |
| 15 | `file`, `prefix_line`, `node`, `header_line`, `block` | |
| 16 | `declaration` | |
| 17 | `actor_decl`, `actor_prop`, `name_prop`, `poses_prop` | Q12 |
| 18 | `enum_decl`, `icon_decl`, `tag_decl`, `tag_entry`, `name_list` | |
| 19 | `var_decl`, `type`, `constant`, `literal` | Q13 |
| 20 | `command_decl`, `command_flag`, `markup_decl`, `function_decl`, `params`, `param` | |
| 21 | `statement` | |
| 22 | `text_line` | Q9, Q14 |
| 23 | `choice`, `option`, `option_modifier` | Q9, Q10 |
| 24 | `if_stmt`, `elif_clause`, `else_clause` | |
| 25 | `set_stmt`, `assign_op`, `flow_stmt`, `wait_stmt` | |
| 26 | `variation`, `alternative`, `alternative_line` | Q15 |
| 27 | `command_stmt`, `arg`, `arg_tail`, `value`, `call_args`, `wait_flag` | Q13, Q16 |
| 28 | `inline_text`, `inline_item` | |
| 29 | `span` | |
| 30 | `point`, `point_body` | Q16 |
| 31 | `cond` | |
| 32 | `expr`, `or_expr`, `and_expr`, `not_expr`, `eq_expr`, `rel_expr`, `add_expr`, `mul_expr`, `unary`, `primary` | Q17 |

### Extension compatibility

The [grammar extensions](design.md#grammar-extensions), and the notes and `required` markup that arrive with localization, only add to this grammar, and none of them needs more than one token of lookahead. The [language design](design.md#grammar) lists each one's changes. These keep that true:

- v1 [reserves](#reserved-words) every word, tag and line marker the extensions use, in the positions the extensions will use them, so no valid v1 story can already mean something by them.
- **Known exception: line show counts.** [Inline variations](design.md#inline-variations) choose their wording from how many times a line has been shown, and v1 doesn't record that. When the extension lands, saves made before it count every line as unseen, so first-time wording (`{once}`) can appear once more for those players. This is cosmetic, never a lost effect or a softlock, and it's accepted rather than storing a count v1 never reads.
- **Known exception: plurals.** The stretch-goal `{plural}` syntax ([localization design](../localization.md#text-direction-plurals-and-formatting)) isn't reserved. Its keyword and case markers (`plural`, `zero`, `one`, `two`, `few`, `many`, `other`) are free in v1, so a story could already use one as a function, term or actor name, and adding plurals could then change what that story means. This is accepted: plurals aren't scheduled, and v1 has few users. When plurals are designed in full, their syntax is chosen to avoid names stories already use, or the change is announced as breaking.
- Some open questions must be decided in a particular direction, or an extension would change what a valid v1 story means:
  - **Q11:** a letter or digit after `\` is an error, so new escapes (`\n`, `\u1234`) can be added.
  - **Q12:** `[`, `{` and `\` in a display name are errors, so display names can later hold markup.
  - **Q16:** decided in a way the mid-line pose extension can keep ([language design](design.md#pose-changes-partway-through-a-line)).
