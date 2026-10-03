# Semantics design

Semantic analysis turns syntax trees into a bound story: it resolves every name, checks every type, and runs the flow, content and style checks.

## Passes

1. **Declaration pass.** Gathers declarations and node names from every file (plus the prelude) into a symbol table, and reports duplicates and reserved-word clashes. [Relative node names](language/reference.md#prefixes) are expanded with their file's prefix here and in binding, so every later stage sees only full names.
2. **Binding pass.** Resolves each node body against the symbol table: actors, poses for a given actor, variables, commands and their arguments, markup, icons, tags, functions, node references, and enum members resolved by expected type. It type-checks expressions and produces a bound tree.
3. **Flow and content checks.** Unreachable statements after `@jump`/`@end`/`@return`, empty choices, missing or duplicate line IDs, and nodes never referenced (reported as information only, because the host starts nodes by name). What option text may contain is a [syntax check](syntax.md#syntax-checks).

The result is exposed as a `SemanticModel`. It answers the questions the language server asks: the symbol at a position, the references to a symbol, and the declaration of a symbol. Designing this API into the model from the start is what makes the language server a thin layer later.

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
- **Configuration** lives in `.editorconfig`, covered in the [tooling design](tooling.md#configuration). Any rule can be turned off or given a different level, and thresholds are settings.
- **Suppressing one instance:** a `// pibbles-ignore PIB5003` comment on its own line directly above the flagged line. At the top of a file, before any node, it applies to the whole file.
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

Flags a statement nested more than three blocks deep. Blocks are `@if`, options, and variation blocks. Deeply nested scripts are hard to follow, and the choice lists inside them get lost.

Non-compliant:

```
-> First option
    @if $a:
        @if $b:
            -> Nested option
                @if $c:
                    This line is four blocks deep.
```

Compliant, with the inner part moved into its own node:

```
-> First option
    @if $a:
        @if $b:
            @call example.inner_choice

== example.inner_choice
-> Nested option
    @if $c:
        This line is now two blocks deep.
```

The quick fix is **Extract to node** (below), applied to the outermost block that brings the nesting back under the limit.

Alternative, combining the conditions:

```
-> First option
    @if $a and $b:
        -> Nested option
            @if $c:
                This line is three blocks deep.
```

#### PIB5002 – Long option body ⚡

**Level:** hint · **Setting:** `pibbles_max_option_body` (default 15 lines)

Flags a choice option whose body is longer than 15 lines. With long bodies, the options of one choice end up screens apart and the choice can't be read as a whole.

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

Flags the same speaker saying the same text in three or more places. Every copy has its own ID, so each one is translated, and possibly recorded, separately. A shared node gets translated and recorded once.

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

Flags the same `[color …]` value used in two or more places. A repeated color usually means something, such as a clue, a warning or a thought. A named markup says what it means, and can be restyled in one place.

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

Flags a pose change that has no visible effect, either because the actor already has that pose or because another change overrides it before any line shows. It only fires when that's certain: in straight-line flow, with no `@call`, choice or jump in between.

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
- a span nested inside the same kind of span
- two identical spans side by side, which could be one

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

Flags `@if` blocks and `{if}` text whose branches are all identical, so the condition makes no difference.

Non-compliant:

```
This line says {if $a}the same thing{else}the same thing{/if}.

@if $a:
    This line appears either way.
@else:
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
@if $has_key == true:
@if $has_key == false:
```

Compliant:

```
@if $has_key:
@if not $has_key:
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

Flags a line or option whose text is too long to read comfortably in one go. Markup and pacing don't count toward the length. For conditional text, the longest branch counts. Long options also crowd the choice menu.

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

Flags names that aren't `snake_case`. That covers nodes (each dot-separated part), actors, poses, personas, variables, enums and their members, commands, markup, icons, terms and functions. Display names like `name: Mira` aren't affected.

Non-compliant:

```
@actor Mira:
@var $HasKey = false
== Kitchen.FrontDoor
```

Compliant:

```
@actor mira:
@var $has_key = false
== kitchen.front_door
```

There's no quick fix. Almost every name is shared with something outside the story. Saves store variable, node, actor and enum-value names, and the game's code refers to commands, markup, icons, tags, functions, actors, poses and nodes. An automatic rename can't update the game's code, and it can't tell whether a name has shipped. This rule catches names as they're first written, before anything depends on them.

Deliberate renames use the editor's **Rename**:
- It always adds a `#was:` alias to renamed nodes, which saves, scenes and game code refer to by name. It doesn't matter whether they've shipped. The `pibbles.rename.aliases` VS Code setting can turn this off.
- For names the game's code uses, the rename preview warns that the code must change too. A renamed command, markup, icon or function shows up as a missing handler when the game launches ([Godot design](godot.md#failures)). Other names in the game's code are the game developer's to update.

#### PIB5032 – Inconsistent indentation width ⚡

**Level:** hint

Flags a file that mixes indentation widths, for example two spaces in one block and four in another. Mixing tabs and spaces is already an error (PIB1001).

Non-compliant:

```
@if $a:
  This block is indented two spaces.
@if $b:
    This block is indented four.
```

Compliant:

```
@if $a:
    Every block is indented four spaces.
@if $b:
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

Flags a variable that nothing in the story ever references. The story owns these, so an unused one is dead weight. A variable that only the game's code uses belongs in the game's own state, not the story's.

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
| Unreferenced nodes | The host starts nodes by name, so "unreferenced" is normal. This is already an info diagnostic in the semantic checks. |
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
