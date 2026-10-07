# Semantics design

Semantic analysis resolves every name in the syntax trees, checks every type, and runs the flow, content and style checks. The binder also keeps what it works out, as the [bindings](#bindings), so the compiler never has to resolve a name or infer a type again.

## Passes

`Compilation.Create` takes a story's sources, as `(path, text)` pairs, parses each file and runs the passes below over all of them together. It never throws on bad input. Its diagnostics, from parsing and analysis alike, are grouped by file in the order the files were given, then ordered by position.

1. **Declaration pass.** Gathers declarations and node names from every file (plus the prelude) into a symbol table, one namespace per kind of name, and reports PIB2021–PIB2023 and PIB2060–PIB2067. [Relative node names](language/reference.md#prefixes) are expanded with their file's prefix here and in binding, so every later stage sees only full names.
   - **The prelude** is Pibbles source built into the core, parsed like any other file: `@markup b`, `i`, `u`, `s` and `color(value: string)`, and `@function visits(target: node) -> number`, which the core handles itself. Its names are [reserved](language/reference.md#reserved-words), so a story can't declare them again, and its symbols have no source location.
   - **Order:** enums are declared first and variables last, across every file, so declaration order never matters. Every type is known before a parameter, function or tag uses it, and a variable whose initial value is a name can be told which type that name belongs to.
   - **One problem, one diagnostic.** A name that's reserved or not ASCII is reported and still declared, so the places that use it don't report it again. A duplicate is reported at the later declaration, which names where the earlier one is, and the earlier one is the one the story uses. A type that doesn't exist is reported once, and whatever has it gets the error type, which nothing reports on.
2. **Binding pass.** Resolves each node body against the symbol table: actors, poses for a given actor, variables, commands and their arguments, markup, icons, tags, functions, node references, and enum members resolved by expected type. It type-checks every expression, including variables' starting values and parameters' defaults.
   - **Expected types.** An expression is bound with the type expected where it appears: a parameter's, a variable's, `bool` for a condition, `duration` for `@wait`. A [bare name](language/reference.md#bare-names) is looked up only among that type's values. For `==` and `!=`, the side that isn't a bare name is bound first, and gives the bare name its type.
   - **Conversions.** A value never changes type, except that a number stands in for a duration where one is expected: in a value of type `duration`, and on the other side of a duration in `+`, `-` and the comparisons. In `*` and `/`, a number is a factor.
   - **Arguments** fill parameters in order, then by name (`at=left`), and a parameter with a default can be left out. Function calls have only positional arguments. A parameter missing its argument isn't reported when another argument's name is unknown, since that argument was most likely meant for it.
   - **Speakers.** A text line's speaker is a declared actor, and its pose one of the actor's poses. A narration line that almost looks like a speaker is reported, never quietly shown: a parenthesis that isn't a single pose (`mira (to Rex):`), and a declared actor with no space after the colon (`mira:Hi`). It reads the line with the parser's own `SpeakerScanner`, so the two never disagree on what a speaker is.
   - **Text** binds markup with its arguments, inline commands (which must be declared `inline`), icons, `{w}` durations, `{if}` conditions, and shown values, which are text, numbers or actors. Tags are declared, and take a value exactly when they're declared with a type.
   - **"Did you mean" suggestions** compare a name only with the names that could go there, such as this actor's poses or this enum's members, by the rule in the [diagnostics catalog](diagnostics.md#ranges). When the name is valid as another kind, the help says so instead: a bare `has_key` in a condition suggests `$has_key`, and `crowbar` where text is expected suggests `"crowbar"`.
   - **The error type** is what anything gets when its type can't be known, because of a problem already reported, such as an unknown variable. It converts to and from every type, and no check reports on it, so one mistake is reported once however deep it sits in an expression.
3. **Flow and content checks.** Statements that never run, options with no text, and missing or duplicate line IDs. What option text may contain is a [syntax check](syntax.md#syntax-checks).
   - **Statements that never run (PIB3001).** A statement always leaves its block when it's `@jump`, `@end` or `@return`, or an `@if` with an `@else` whose every branch always leaves. The first statement after one in the same block is reported, once per block. It's decided from the statements alone: conditions are never evaluated and values never tracked, so `@if false` is like any other condition. Choices and variations never count as leaving, since a choice is skipped when no option is available and a variation's block can be skipped too.
   - **Options with no text (PIB3002)** are information, not errors. The story runs, and the host decides what an empty option looks like, so a game that wants one turns the code off.
4. **Style checks.** The [style rules](#style-rules), over the syntax trees, with the symbol table for actors and the recorded references for unused variables. Each file's thresholds come from its settings. Most rules look at one file; repeated lines (PIB5003) and repeated colors (PIB5004) count across the story, and each place is reported when the count reaches its own file's threshold.

Once every pass has run, each diagnostic gets the severity its file's settings give it, or is dropped when they turn it off, and then [`// pibbles-ignore`](#levels-and-visibility) comments silence what they cover. Last, style hints on a line that has an error are dropped.

### Bindings

`Compilation.Bindings` is internal, for the compiler. The binder fills it as it goes:

- **The symbol each name refers to.** Names are keyed by the syntax node that spells them: the name of a speaker, pose, command, markup, icon, called function, named argument, or `@jump` and `@call` target; a variable; a bare name (an enum member, an actor or a node); and a tag, whose enum value is kept separately. A name that doesn't resolve has no entry.
- **Each expression's type, and its converted type.** Every expression the binder reaches has an entry, nested ones included, and variables' starting values and parameters' defaults too. The converted type is the type the context uses the expression as, which differs from its type only for a number where a duration is expected (`@wait 1`, `{w 1}`, a `duration` argument, `@set $d += 1`, the number beside a duration in `+`, `-` and the comparisons). An expression whose type can't be known has the error type.

- **The parameter each argument fills,** for commands (statement and inline), markup and function calls, positional and named alike, keyed by the argument's value expression. An argument that fills no parameter (an extra one, a repeat, or one for something that doesn't exist) has no entry.
- **Defaults and starting values.** A `ParameterSymbol` keeps the expression after its `=`, and a `VariableSymbol` its starting value (the first declaration's, for a duplicate). Both are bound like any other expression, so their types and conversions are in the bindings.

Entries are keyed by the identity of the syntax node, never its value, because syntax nodes compare by value and two identical lines at the same position in different files are different nodes. The bindings take no part in diagnostics, and the [semantic model](#the-semantic-model) is separate: it answers the editor's questions by position, and the bindings answer the compiler's by node.

### The semantic model

`Compilation.Model` is a `SemanticModel`. It answers the questions the language server asks, and designing it in from the start is what makes the language server a thin layer later:

- **`GetSymbolAt(path, position)`** gives the symbol a name refers to, where it's used or where it's declared. A position just past a name's end still counts, as a cursor there does.
- **`FindReferences(symbol)`** gives every use of a symbol, not counting its declaration, which is the symbol's `Location`. Built-in symbols have no location.
- **`Symbols<T>()`** lists the symbols of one kind, the built-in ones included, for completion. Poses, enum members and parameters are listed along with what they belong to.

The passes record every name that refers to a symbol as they resolve it: speakers, poses, commands and named parameters, markup, icons, tags and enum values in tags, functions, variables, bare names, node references, and enums named as types. A node's old names in `#was:` count as declarations of the node, and a use of an old name refers to the node itself. The symbols are public, and everything that builds them stays internal.

## Style rules

Style rules flag scripts that work correctly but could be clearer, easier to translate or easier to maintain. They never flag mistakes, which are the job of errors and warnings. They only nudge.

### Levels and visibility

Style rules introduce a fourth severity below the existing three:

| Level | Meaning | CLI (`pibbles check`) | VS Code |
| --- | --- | --- | --- |
| Error | Broken. The story won't build. | Always shown | Red squiggle, Problems panel |
| Warning | Probably a mistake | Always shown | Yellow squiggle, Problems panel |
| Info | Worth knowing, such as maintenance issues | Shown | Blue squiggle, Problems panel |
| **Hint** | **Pure style** | Hidden unless `--style` | Faint dots under the text, not in the Problems panel |

- Style rules use the `PIB5xxx` range. Most are hints. Maintenance rules are info.
- `--warnaserror` never promotes hints or info, so style never blocks a build.
- **A line with an error gets no style hints.** The error comes first, and a hint about code that doesn't work yet is noise: `@if $has_kye == true` reports the unknown variable, not the comparison.
- **Configuration** lives in `.editorconfig`, covered in the [tooling design](tooling.md#configuration). Any rule can be turned off or given a different level, and thresholds are settings.
- **Suppressing one instance:** a `// pibbles-ignore PIB5003` comment on its own line directly above the flagged line, with only other comments between them. It can list several codes. Above a file's first node, it applies to the whole file. Only style and spelling can be silenced this way, since a mistake gets fixed rather than hidden.
- **Quick fixes:** every rule whose fix is mechanical offers one in VS Code. Those rules are marked ⚡.

### What makes a good style rule

A rule is proposed only if:

1. **There's always a better alternative in the language,** so the rule can say exactly what to do instead.
2. **It rarely fires on deliberate choices.** A style rule that's usually wrong trains people to ignore all of them.
3. **It can be explained in one sentence.**

Rules that failed these tests are listed [at the end](#considered-and-left-out).

---

### Structure and reuse

#### PIB5001 – Deep nesting ⚡

**Level:** hint · **Setting:** `pibbles_max_nesting` (default 3)

Flags a statement nested more than three blocks deep. Blocks are `@if`, options, and variation blocks. Deeply nested scripts are hard to follow, and the choice lists inside them get lost. Only the first line of each block past the limit is flagged, not every line in it or deeper.

Non-compliant:

```
-> First option
    @if $a
        @if $b
            -> Nested option
                This line is four blocks deep.
```

Compliant, with the inner part moved into its own node:

```
-> First option
    @if $a
        @if $b
            @call example.inner_choice

== example.inner_choice
-> Nested option
    This line is now one block deep.
```

The quick fix is **Extract to node** (below), applied to the outermost block that brings the nesting back under the limit.

Alternative, combining the conditions:

```
-> First option
    @if $a and $b
        -> Nested option
            This line is three blocks deep.
```

#### PIB5002 – Long option body ⚡

**Level:** hint · **Setting:** `pibbles_max_option_body` (default 15 lines)

Flags a choice option whose body is longer than 15 lines, not counting blank lines and comments. With long bodies, the options of one choice end up screens apart and the choice can't be read as a whole.

Non-compliant:

```
-> First option
    This body goes on for many lines.
    …
    (twenty lines later)
-> Second option
```

Compliant:

```
-> First option
    @jump example.first_branch
-> Second option

== example.first_branch
This body now lives in its own node.
…
```

The quick fix is **Extract to node**, applied to the option body.

##### Extract to node

A refactoring that PIB5001 and PIB5002 offer as a quick fix, and that works on any selected block of statements:

- **It asks for a name** for the new node, placed directly after the current one.
- **It moves the statements** into the new node and leaves a `@call` in their place. If every path through the moved block ends in `@jump`, `@end` or `@return`, it leaves a `@jump` instead, since control never comes back.
- **Moved lines and variation blocks keep their IDs,** so translations, recordings, chosen-option state and how often each block has run all follow them. The new `@call` gets a fresh ID.
- **Old saves resume in the new node,** because moved lines keep their IDs. Such a save has no call frame for the new `@call`, so the dialogue ends when the new node does, instead of returning. [Release manifests](#with-extensions) close that gap.

#### PIB5003 – Repeated line

**Level:** hint · **Setting:** `pibbles_min_repeated_lines` (default 3)

Flags the same speaker saying the same text in three or more places, narration included. Every copy has its own ID, so each one is translated, and possibly recorded, separately. A shared node gets translated and recorded once.

Non-compliant:

```
== room.a
mira: This exact line appears in several nodes.

== room.b
mira: This exact line appears in several nodes.

== room.c
mira: This exact line appears in several nodes.
```

Compliant:

```
== common.shared_line
mira: This exact line appears in several nodes.

== room.a
@call common.shared_line
```

#### PIB5004 – Repeated raw color

**Level:** hint · **Setting:** `pibbles_min_repeated_colors` (default 2)

Flags the same `[color …]` value used in two or more places, ignoring capital letters. A repeated color usually means something, such as a clue, a warning or a thought. A named markup says what it means, and can be restyled in one place.

Non-compliant:

```
This line marks [color "#ff8800"]a phrase[/color] in orange.
This line marks [color "#ff8800"]another phrase[/color] the same way.
```

Compliant:

```
@markup clue

This line marks [clue]a phrase[/clue] as a clue.
This line marks [clue]another phrase[/clue] the same way.
```

There's no quick fix. A new markup only looks right once the game implements its styling, so this is a change for a writer and a programmer together. The hint says so. Once the markup exists in the game, replacing the colors is a find-and-replace.

---

### Redundancy

#### PIB5010 – Redundant pose change ⚡

**Level:** hint

Flags a pose change that has no visible effect, either because the actor already has that pose or because another change overrides it before any line shows. It only fires when that's certain: in straight-line flow, with nothing but lines, `@set` and `@wait` in between. A command, `@call`, choice, jump or block might change a pose or skip ahead, so after one nothing is known. A `@wait` shows the pose, so a pose-only line before one is seen.

Non-compliant:

```
mira (happy): This line sets the pose.
mira (happy): The pose on this line changes nothing.

mira (sad):
mira (happy): The silent pose change above is never seen.
```

Compliant:

```
mira (happy): This line sets the pose.
mira: This line keeps it.

mira (happy): Only the pose that's actually seen is written.
```

#### PIB5011 – Redundant pause ⚡

**Level:** hint

Flags pauses that do nothing or could be one:
- `{w}` at the very end of a line. The end of a line already waits for a click.
- `{w}` directly before `{p}`. The page break already waits.
- Timed pauses right next to each other.

Timed pauses at the end of a line aren't flagged, because they add a delay in auto mode.

Non-compliant:

```
This line waits for a click that happens anyway.{w}
This line waits twice here.{w}{p}New page.
This line pauses{w 0.2}{w 0.3} in two steps.
```

Compliant:

```
This line ends normally.
This line waits once here.{p}New page.
This line pauses{w 0.5} in one step.
```

#### PIB5012 – Redundant markup ⚡

**Level:** hint

Flags markup that does nothing:
- empty spans
- a span nested inside one just like it
- two like spans side by side, which could be one

Spans are alike when they have the same name and the same arguments, so a `[color]` inside a different color isn't flagged.

Non-compliant:

```
This line has an empty span: [b][/b]
This line is [b]bold and [b]still just bold[/b][/b].
This line is [b]split into[/b][b] two spans[/b].
```

Compliant:

```
This line has no empty span.
This line is [b]bold and still just bold[/b].
This line is [b]one span[/b].
```

#### PIB5013 – Redundant condition ⚡

**Level:** hint

Flags `@if` blocks and `{if}` text whose branches are all identical, so the condition makes no difference. It needs an `@else` or `{else}`, since without one the branch differs from showing nothing. Branches compare by their text, apart from indentation, line IDs, blank lines and comments, which never change what runs.

Non-compliant:

```
This line says {if $a}the same thing{else}the same thing{/if}.

@if $a
    This line appears either way.
@else
    This line appears either way.
```

Compliant:

```
This line says the same thing.
This line appears either way.
```

#### PIB5014 – Redundant `@return` ⚡

**Level:** hint

Flags `@return` as the last statement of a node. Reaching the end of a node returns anyway.

Non-compliant:

```
== common.shared
This line is shared.
@return
```

Compliant:

```
== common.shared
This line is shared.
```

`@return` earlier in a node, for example inside an `@if`, is fine.

---

### Expressions

#### PIB5020 – Comparing with `true` or `false` ⚡

**Level:** hint

Non-compliant:

```
@if $has_key == true
@if $has_key == false
```

Compliant:

```
@if $has_key
@if not $has_key
```

#### PIB5021 – Use `+=` and `-=` ⚡

**Level:** hint

Non-compliant:

```
@set $attempts = $attempts + 1
@set $attempts = $attempts - 1
```

Compliant:

```
@set $attempts += 1
@set $attempts -= 1
```

---

### Readability

#### PIB5030 – Long text

**Level:** hint · **Settings:** `pibbles_max_message_length` (default 300 characters), `pibbles_max_option_length` (default 80)

Flags a line or option whose text is too long to read comfortably in one go. Markup and pacing don't count toward the length, an icon counts as one character, and a value shown with `{…}` counts nothing, since its length isn't known. For conditional text, the longest branch counts. A line counts its longest page, since `{p}` starts a new one. Long options also crowd the choice menu.

Non-compliant:

```
mira: This line keeps going and going, well past the point where it fits comfortably in a text box, and the player has to read an entire paragraph before clicking, which is tiring in a visual novel…
-> This option is written as a full sentence that explains everything that will happen if it's picked
```

Compliant:

```
mira: This line makes its point.
mira: The next line continues it.
-> Short option
```

Alternative, keeping one message (and one voice clip) but splitting it into pages:

```
mira: This line makes its point.{p}The rest appears on the next page.
```

#### PIB5031 – Naming convention

**Level:** hint

Flags names that aren't `snake_case`, where they're declared. That covers nodes (each dot-separated part) and the file's prefix, actors, poses, personas, variables, enums and their members, commands, markup, icons, terms and functions. Display names like `name: Mira` aren't affected.

Non-compliant:

```
@actor Mira
@var $HasKey = false
== Kitchen.FrontDoor
```

Compliant:

```
@actor mira
@var $has_key = false
== kitchen.front_door
```

There's no quick fix. Almost every name is shared with something outside the story. Saves store variable, node, actor and enum-value names, and the game's code refers to commands, markup, icons, tags, functions, actors, poses and nodes. An automatic rename can't update the game's code, and it can't tell whether a name has shipped. This rule catches names as they're first written, before anything depends on them.

Deliberate renames use the editor's **Rename**:
- It always adds a `#was:` alias to renamed nodes, which saves, scenes and game code refer to by name. It doesn't matter whether they've shipped. The `pibbles.rename.aliases` VS Code setting can turn this off.
- For names the game's code uses, the rename preview warns that the code must change too. A renamed command, markup, icon or function shows up as a missing handler when the game launches ([Godot design](godot.md#failures)). Other names in the game's code are the game developer's to update.

#### PIB5032 – Inconsistent indentation width ⚡

**Level:** hint

Flags a file that mixes indentation widths, for example two spaces in one block and four in another. Each block is measured from the block it's in. With `indent_size` in `.editorconfig`, blocks indented with spaces follow it, and otherwise the file's first block sets the width. A file indented with a different character than `indent_style` asks for is flagged once, at its first indented line. Mixing tabs and spaces is already an error (PIB1001).

Non-compliant:

```
@if $a
  This block is indented two spaces.
@if $b
    This block is indented four.
```

Compliant:

```
@if $a
    Every block is indented four spaces.
@if $b
    Every block is indented four spaces.
```

#### PIB5033 – Speaker spacing ⚡

**Level:** hint

Flags a speaker prefix with spacing other than `name (pose):`. Any spacing works, but one form keeps scripts easy to scan.

Non-compliant:

```
mira(happy): This line has no space before the pose.
mira ( happy ): This line has spaces inside the parentheses.
mira : This line has a space before the colon.
```

Compliant:

```
mira (happy): This line uses the usual spacing.
mira: So does this one.
```

---

### Maintenance

#### PIB5040 – Unused declaration

**Level:** info

Flags a variable that nothing in the story ever references. Setting it counts as a reference. The story owns these, so an unused one is dead weight. A variable that only the game's code uses belongs in the game's own state, not the story's.

The game's vocabulary isn't checked: commands, markup, icons, tags, functions, actors, poses and enum members. The game offers those as a toolbox, and it's normal for part of a toolbox to go unused.

Non-compliant:

```
@var $old_flag = false
```

Here nothing ever references `$old_flag`.

Compliant: remove it, or use it.

---

### Considered and left out

| Idea | Why not |
| --- | --- |
| Long nodes | Visual novel scenes are long by nature, so the rule would mostly fire on well-written scenes |
| A choice with a single option | Often deliberate, for pacing (`-> Continue`) |
| Any raw color, even used once | One-off colors are legitimate. Repeats are the real signal (PIB5004). |
| Unreferenced nodes | The host starts nodes by name, so "unreferenced" is normal, and only the host knows which nodes it starts. An engine-agnostic core can't learn that from one engine's adapter. If a need comes up, the host passes its entry nodes to the compilation and the check returns as a warning. Until then, Find References shows a node nothing uses. |
| Variables that are set but never read | The game's code often reads them (achievements, UI), so there would be too many false alarms |
| A missing `///` note on a line with conditional text | Whether a note is needed is a judgement call |

## With extensions

What the [grammar extensions](language/design.md#grammar-extensions) and later tiers ([roadmap](roadmap.md#feature-tiers)) add to the style rules.

- **Localization:** PIB5004's hint also suggests marking the new markup `required`, so no translation loses it.
- **Terms:** PIB5040 also flags unused terms, and PIB5005 (below) joins the rules.
- **Release manifests:** renaming a variable also adds a `#was:` alias to its `@var`. Extract to node's new `@call` gets restored for old saves: a save made inside the moved block has no call frame for it, so restoring adds that frame, and the dialogue still returns where it used to continue ([runtime design](runtime.md#release-manifests-and-migrations)).

### PIB5005 – Repeated inline condition ⚡ (with terms)

**Level:** hint · **Setting:** `pibbles_min_repeated_conditions` (default 2)

Flags the same `{if}…{/if}` appearing in two or more lines. That's what terms are for: written once, translated once, fixed once.

Non-compliant:

```
{if $pronouns == he}He{elif $pronouns == she}She{else}They{/if} opened this line.
Later, {if $pronouns == he}he{elif $pronouns == she}she{else}they{/if} closed it.
```

Compliant:

```
@term they = {if $pronouns == he}he{elif $pronouns == she}she{else}they{/if}

{They} opened this line.
Later, {they} closed it.
```

Occurrences that differ only in capitalization count as the same, and the quick fix uses `{They}` where needed.
