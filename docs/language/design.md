# Language design

Why the `.pib` language is shaped the way it is, and the features designed for it that wait for a need. The [language reference](reference.md) is authoritative for the language as it is. When an extension below is built, its design, grammar and guide text move into the reference and the [writer's guide](guide.md) in the same change.

## What Pibbles is

Pibbles is a narrative scripting language for visual novels, plus a .NET library that compiles and runs it. Writers put dialogue, choices, branching logic and stage directions in plain-text `.pib` files, usually under a `story/` folder in the game repository. The game loads the story and asks Pibbles what happens next. Pibbles answers with lines to show, choices to offer and commands to carry out. The game presents them however it likes.

People often call this kind of language a templating language, but that's only half right. Pibbles has two layers:

- **Flow layer.** Statements that control *which* lines run: branching, choices, jumps, variables and visit counts. This is a small state machine language, closer to Ink or Yarn Spinner than to Liquid.
- **Text layer.** Inline markup that controls *how a single line reads*: styling, interpolation, conditional wording, pauses and events tied to a spot in the text. This part is templating.

Keeping the two layers separate is deliberate. It shapes the syntax ([reference](reference.md)) and the translation model ([localization design](../localization.md)).

### What it's built for

Pibbles targets games where dialogue is woven into other gameplay, such as exploration or puzzles. That sets several priorities:

- **Lots of short, re-entrant dialogues.** A game might start a small dialogue every time the player inspects an object, and the same object gets inspected five times. So visit counts, "first time only" blocks and line variations are core features, not nice-to-haves.
- **The game owns the main loop.** Pibbles never takes control. The game starts a node, steps through it and hands control back to its own gameplay. The story and the game share state through variables and host functions.
- **Heavy branching.** Typos in variable names, jump targets and command names are the most common way branching scripts break. Catching them before the game runs is worth a lot.

Pibbles itself knows nothing about any engine or genre. Godot is the first supported engine, through a thin adapter.

## Goals

1. **Writer-first syntax.** A page of Pibbles should read like a screenplay with a few annotations. Writers aren't programmers, and the common case (a character says a line) needs no ceremony.
2. **Catch mistakes while editing.** Anything that can be checked statically is checked: unknown actors, poses, commands, argument types, variables, jump targets, unclosed markup, broken translations. The same checks power the CLI, CI and the editor.
3. **Engine-agnostic core.** The core library depends only on the .NET base class library. It knows nothing about Godot, rendering, audio or input. Engine integration is a thin adapter.
4. **Rich enough for a real VN.** Styled text, timed pauses, variable speed, events at exact spots in a line, voice lines, choices, conditions, variations, subroutines and stage directions.
5. **Localization-ready from the first line.** Stable line IDs and a strict model for what a translation may and may not change are part of the foundation, even if the translation tooling ships later.
6. **Testable without an engine.** Every language feature can be exercised from a unit test or the CLI with no game running. This matters as much for coding agents as for people.
7. **Small and maintainable.** A small keyword set, one way to do each thing, and an open vocabulary supplied by the host instead of a growing list of built-ins.

## Non-goals

- **Rendering, audio, animation or input.** Pibbles never draws a pixel or plays a sound. See [boundaries](../boundaries.md).
- **A visual or node-graph editor.** Text files in a normal editor, with language-server support.
- **General-purpose programming.** No loops, no user-defined functions, no objects. Reuse happens through `@call`. Complex logic belongs in game code and is exposed through host functions.
- **Full ICU MessageFormat or CLDR coverage.** Pluralization is a stretch goal with a narrow scope ([localization design](../localization.md#text-direction-plurals-and-formatting)).
- **Rollback or rewind** in the Ren'Py sense. The state model doesn't rule it out later.
- **Runtime modding or untrusted scripts.** Scripts are trusted game content.

## Design principles

These are the tie-breakers for decisions the docs don't cover.

1. **Text first.** An unmarked line is dialogue or narration. Every piece of logic is marked with `@` (statements) or sits inside `{}` / `[]` (inline). A writer never has to escape a normal sentence because it happens to start with *If*.
2. **The story says what and when; the host says how.** Pibbles decides that Mira jolts at the fourth character of this line. The game decides what a jolt looks like.
3. **Declared, therefore checked.** Everything the host provides (actors, poses, commands, markup, icons, tags, enums, functions) is declared in `.pib` files. The declarations are the contract between writers and programmers, and the analyzer enforces it.
4. **Fail at edit time, not play time.** A problem the analyzer could have found should never first show up in a playtest.
5. **Effects are sacred, information is protected, presentation is negotiable.** A translation must keep every effect (commands, pose changes) and every piece of player-facing information (icons, required markup). It may freely re-time pauses and move emphasis. See the [localization design](../localization.md#what-a-translation-may-change).
6. **Line-oriented and error-tolerant.** Each source line is classified on its own. A mistake on one line produces one diagnostic and never cascades. This is what makes live editor feedback practical.
7. **Deterministic: no hidden inputs.** Output depends only on the story, the saved state and explicit inputs: the player's choices, host function results and the locale. Randomness comes from a seeded generator stored in the state. So a transcript replays exactly, a save reloads without rerolling, and skipping never changes the outcome.
8. **Small language, open vocabulary.** New needs are met by host declarations, not new keywords. Adding a keyword needs a strong reason.
9. **Portable across Godot projects.** No reflection, and safe to trim and compile ahead of time, because some Godot export targets use NativeAOT. The core targets the oldest .NET version that Godot's C# packages support, as well as the newest.

## Why build it

Pibbles exists because designing it is the fun part, and because a library built around a real game's needs can fit them exactly. It is open source and anyone is welcome to use it, but it doesn't try to compete with mature tools like [Yarn Spinner](https://www.yarnspinner.dev/) or [Ink](https://www.inklestudios.com/ink/), and its docs don't pitch it as an alternative to them. If someone needs a proven dialogue system today, those are the better choice.

Pibbles borrows ideas freely from both:

- **From Yarn Spinner:** `->` options, `#` line tags, and localization keyed by line IDs.
- **From Ink:** sequence, cycle and shuffle variations, and visit counts.

What Pibbles is tailored for:

- **A fully declared, statically checked contract with the host.** Commands have typed parameters, and poses, markup and icons are all declared and checked while editing.
- **A strict translation model.** Translations are checked element by element against the source: effects must be preserved, presentation is free.
- **An inline timing model** built for visual-novel pacing.

## Glossary

| Term | Meaning |
| --- | --- |
| **Story** | All `.pib` files of a project, compiled together. |
| **Node** | A named, addressable unit of content (`== kitchen.door`). The host starts nodes, and nodes jump to and call each other. |
| **Line** | One message shown to the player: an optional speaker, text with inline markup, and tags. |
| **Option** | One entry of a choice (`-> Use the key`). Consecutive options form a **choice**. |
| **Actor** | A declared character who can speak and be staged. |
| **Pose** | A named sprite variant of an actor: facial expressions, body poses, anything the host swaps. `mira (smug):` sets Mira's pose to `smug`. "Pose" rather than "expression" avoids a clash with code expressions. |
| **Persona** | An alternate presentation of an actor that turns on automatically from story state, such as "???" before Rex is introduced, or Mira in disguise. The actor's identity stays the same. ([Personas](#personas) are a grammar extension.) |
| **Command** | A host-declared instruction (`@shake_screen`, `{@sfx thud}`) that Pibbles schedules and the host carries out. |
| **Markup** | A host-declared or built-in styling range in text (`[clue]…[/clue]`). |
| **Tag** | Metadata at the end of a line, option, `@call` or node header (`#id:k7qp2x`, `#thought`). Node headers take only reserved tags such as `#was`. Tags are never shown to the player. |
| **Host** | The program running the story, such as a game, the CLI player or a test. |
| **Adapter** | Engine-specific glue between the core and an engine (for example `Pibbles.Godot`). |

## Grammar extensions

Each extension below is fully designed, and joins the language when its trigger comes up. Adding one follows the "Adding or changing a language construct" checklist in [AGENTS.md](../../AGENTS.md#workflows): its design and grammar move into the [reference](reference.md), its guide text into the [writer's guide](guide.md), and each subsystem's "With extensions" section moves into that document's body.

| Extension | Trigger |
| --- | --- |
| [`@shuffle`, `@shuffle once`, `random()`](#shuffle-shuffle-once-and-random) and the seeded generator | The first need for randomness |
| [`@else` after `@once:` and `@shuffle once:`](#else-after-once-and-shuffle-once) | A "first time X, afterwards Y" pattern that `visits()` handles awkwardly |
| [Inline `{sequence}`, `{cycle}`, `{shuffle}`, `{once}`](#inline-variations) | Block forms feel too heavy for varying a few words |
| [Pose changes partway through a line](#pose-changes-partway-through-a-line) (`{(pose)}`, `{actor (pose)}`) | A pose change that must land mid-line |
| [`{auto}`](#auto) | Interrupted speech |
| [Personas](#personas) | The first mystery character or disguise |
| [Terms](#terms) | Player pronouns, or other wording that depends on state in many places |
| [`speaker` parameter defaults](#speaker-parameter-defaults) | Voice barks |
| [Drafts](#drafts), with development and release build profiles | Content in the repository that must not ship |
| [Release manifests, `#was:` on lines and `@var`, migrations and `@resume`](#migrations), `pibbles release` | Before the first public release of a game that saves mid-dialogue, since only then do old saves exist |

[Notes](#notes) and [`required` markup](#required-markup) aren't triggered by a need: they arrive with the localization pipeline ([roadmap](../roadmap.md#feature-tiers)). [Voice tooling](#voice) is a stretch goal.

### Grammar

The full grammar is the [v1 grammar](reference.md#appendix-grammar) plus the changes below. v1 already [reserves](reference.md#reserved-words) every word, tag and line marker they use, so each change either adds an alternative behind a new keyword or token, or makes valid something v1 rejects. None changes what a valid v1 story means, and none needs more than one token of lookahead.

| Extension | Kind | Changes |
| --- | --- | --- |
| `@shuffle`, `@shuffle once`, `random()` | Additive | `variation` |
| `@else` after `@once:` and `@shuffle once:` | Additive: v1 reports a stray `@else` | `variation` |
| Inline variations | Additive, with lexer changes | `inline_item`; new `ivariation`, `alternatives`, `alternative_text`; four more fused `{` tokens; `\|` becomes a token |
| Pose changes partway through a line | Additive: v1 rejects `{name (…)}` | `point_body` |
| `{auto}` | Additive | `point_body` |
| Personas | Additive: v1 reports an unknown actor property | `actor_prop`; new `persona_prop` |
| Terms | Additive | `declaration`, `point_body`; new `term_decl` |
| `speaker` parameter defaults | Additive | `constant` |
| Drafts | None: `#draft` is a tag | |
| Release manifests, `#was:` on lines and `@var`, migrations and `@resume` | Additive: v1 accepts `#was:` only on node headers | `var_decl`, `flow_stmt`; new claim grammar inside `#migrates:` values |
| Notes (with localization) | Gives the NOTE line class, reserved in v1, a meaning | Line classification only |
| `required` markup (with localization) | Additive: v1 reports the extra word | `markup_decl` |

Each section below shows the changed productions in full. Unchanged alternatives are written as `…`.

### `@shuffle`, `@shuffle once` and `random()`

Two more variation blocks join `@sequence`, `@cycle` and `@once`, with their rows in the reference's [variations](reference.md#variations) table:

| Block | Runs |
| --- | --- |
| `@shuffle:` | A random alternative, using each one once per round. Round `n div count`, position `n mod count` in that round's order ([Q21](#open-questions)) |
| `@shuffle once:` | Like `@shuffle` while `n < count`; after the first round it's skipped |

```
@shuffle:
    - mira: Dusty.
    - mira: Very dusty.
        @sfx sneeze
        mira: Ugh.
```

- **`random(min, max) -> number`** is a built-in function: an inclusive integer from the seeded random generator.
- **`random()` only runs in statements** that execute once: `@set`, `@if`, `@elif`, `@wait` and command arguments. Inline text, terms, option conditions and persona conditions are evaluated again when a restored line or choice is shown, or after every state change, so a roll there would change on load. To use a random value in text, roll it into a variable with `@set` first.
- **Shuffle state and the random generator's state** live in the story state and are saved with it ([runtime design](../runtime.md#randomness)).

**Grammar:**

```
variation       ::= ("@sequence" | "@cycle" | "@shuffle" "once"?) ":" TAG* EOL INDENT alternative+ DEDENT
                  | "@once" ":" TAG* EOL block
```

The token after `@shuffle` tells `@shuffle:` from `@shuffle once:`. `random()` is an ordinary call. Allowing it only in statements that run once is a check.

**Guide text:**

> | Block | Behavior |
> | --- | --- |
> | `@shuffle:` | Picks at random, using each option once before any repeats |
> | `@shuffle once:` | Picks at random, using each option once, then is skipped |
>
> #### Randomness
>
> `random(1, 6)` gives a whole number from 1 to 6:
>
> ```
> @set $roll = random(1, 6)
> The roll was {$roll}.
> @if random(1, 100) <= 25:
>     This line appears about a quarter of the time.
> ```
>
> Randomness is saved with the game, so loading a save doesn't reroll anything.
>
> `random` works in `@set` and `@if`, but not inside a line's text. To show a random number, store it in a variable first, as above.

### `@else` after `@once:` and `@shuffle once:`

`@once:` and `@shuffle once:` can be followed by an optional `@else:`, which runs each time once the block has nothing left. Without it, a used-up block shows nothing. Like `@if`, the `@else` belongs to the block directly above it at the same indentation. `@sequence`, `@cycle` and `@shuffle` never run out, so an `@else` after them is an error.

```
@shuffle once:
    - mira: Spoons.
    - mira: More spoons.
@else:
    mira: Still just spoons.
```

**Grammar:**

```
variation       ::= ("@sequence" | "@cycle" | "@shuffle" "once"?) ":" TAG* EOL INDENT alternative+ DEDENT else_clause?
                  | "@once" ":" TAG* EOL block else_clause?
```

`@else` starts no statement, so the parser can't mistake it for whatever follows the block. The grammar accepts it after every variation, and rejecting it after `@sequence`, `@cycle` and `@shuffle` is a check, which gives that error its own diagnostic.

**Guide text:**

> To say something else once `@once:` or `@shuffle once:` has run out, add `@else:` directly below it:
>
> ```
> @shuffle once:
>     - This line is picked at random.
>     - So is this one.
> @else:
>     This line is shown every time after both have been used.
> ```

### Inline variations

```
mira (sad): {cycle}Nope.|Not that.|That's not it either.{/cycle}
mira: {once}Oh! {/once}A note.
mira: {once}A note!{else}The note again.{/once}
```

- `{sequence}`, `{cycle}`, `{shuffle}`, `{shuffle once}` and `{once}` work like their block forms, with alternatives separated by `|`. `{once}` has a single alternative. `{once}` and `{shuffle once}` accept an optional `{else}` before the closing tag (`{/once}`, `{/shuffle}`), shown once they've run out. As with blocks, `{else}` in any other variation is an error.
- **Selection depends on how many times this line has been shown,** which the state tracks per line ID from this extension on. Saves made before it count every line as unseen ([known exception](reference.md#extension-compatibility)). It doesn't depend on counters for each variation. So a translation can have a different number of alternatives without desynchronizing saves ([localization design](../localization.md#what-a-translation-may-change)).
- **`{shuffle}` never draws from the random generator.** Each round's order is derived from the state's seed, the line ID and the round number. So a translation with a different number of alternatives doesn't shift any later random result.
- Variations are allowed in text lines and option text. Spans can't cross a variation boundary.
- `|` becomes a special character in text, escaped as `\|`.

**Grammar:** the lexer changes in two ways:

- `{` also fuses with `sequence`, `cycle`, `shuffle` and `once`, into BRACE_SEQUENCE, BRACE_CYCLE, BRACE_SHUFFLE and BRACE_ONCE. Without this, a variation and a point would both start with BRACE.
- `|` becomes a PIPE token, where v1 reads it as text.

```
inline_item     ::= … | PIPE | ivariation
ivariation      ::= (BRACE_SEQUENCE | BRACE_CYCLE | BRACE_SHUFFLE "once"?) "}" alternatives
                    (BRACE_ELSE "}" inline_text)? BRACE_CLOSE vkind "}"
                  | BRACE_ONCE "}" inline_text (BRACE_ELSE "}" inline_text)? BRACE_CLOSE vkind "}"
vkind           ::= "sequence" | "cycle" | "shuffle" | "once"
alternatives    ::= alternative_text (PIPE alternative_text)*
alternative_text ::= (inline_item - PIPE)*                               /* Q18 */
```

- PIPE is text everywhere except directly inside `alternatives`, so a `|` in v1 text keeps its meaning.
- An `{else}` inside a variation belongs to the innermost open construct, because every construct has its own closing tag.
- Checks: `{else}` only in `{once}` and `{shuffle once}`, and the closing tag matches the opening one (`{/shuffle}` closes both `{shuffle}` and `{shuffle once}`).

**Guide text:**

> The same idea works inside a single line, with options separated by `|`:
>
> ```
> mira: {cycle}First wording.|Second wording.|Third wording.{/cycle}
> mira: {once}This part only appears the first time. {/once}This part always appears.
> mira: {once}This line appears the first time.{else}This line appears every time after.{/once}
> ```

### Pose changes partway through a line

`{(pose)}` changes the speaker's pose at this point in the line, and `{actor (pose)}` changes another actor's. They're effect markers: allowed in text lines but not option text, and fired by the reveal helper like inline commands, including when a reveal is skipped.

**Grammar:**

```
point_body      ::= … | "(" NAME ")" | NAME "(" NAME ")"
```

`{(pose)}` starts with a token no v1 point can start with. `{actor (pose)}` is told apart from a call by the space: a call's `(` touches its name and lexes as CALL_OPEN, while the `(` in `{mira (happy)}` is a plain `(` ([tokens](reference.md#tokens)). v1 already rejects `{fn (x)}`, so the extension only gives a meaning to something v1 rejects.

**Guide text:**

> ```
> mira: This part is said with her current pose,{(sad)} and this part with the "sad" pose.
> mira: This line changes Rex's pose partway through.{rex (surprised)}
> ```

### `{auto}`

`{auto}` is only valid at the very end of a line: the dialogue advances automatically once the reveal finishes, for interrupted speech. It's an effect, allowed in text lines but not option text.

**Grammar:**

```
point_body      ::= … | "auto"
```

`{auto}` being last on its line is a check.

**Guide text:**

> ```
> This line moves on by itself as soon as it finishes typing.{auto}
> ```
>
> | Write | Effect |
> | --- | --- |
> | `{auto}` | At the very end of a line: move on without waiting for a click. Good for someone being interrupted. |

### Personas

A persona is an alternate presentation of an actor that turns on automatically from story state. Use one for a mystery character before they're introduced, or for a disguise.

```
@actor rex:
    name: Rex
    poses: neutral, grumpy, surprised
    persona stranger if not $met_rex:
        name: ???
```

- **Writers always write the real actor.** Every line says `rex:`. While `$met_rex` is false, those lines show "???" as the speaker name. Revealing Rex is just `@set $met_rex = true`, wherever and in however many branches that happens. Nothing else in the script changes.
- **A persona can override `name`.** Everything else a persona changes, such as silhouette sprites, a different nameplate color or a masked voice, is presentation and belongs to the host, keyed by the actor ID and the persona name.
- **Conditions are checked in declaration order, and the first true one wins.** When none are true, the actor uses its base presentation (persona `default`). Conditions follow the same rules as any expression.
- **The runner re-checks personas after every state change.** It emits a persona step whenever an actor's active persona changes, so the host can swap sprites that are already on screen.
- **The speaker's display name,** and an interpolated actor's name, come from the actor's active persona.
- Persona names are localized like actor names ([localization design](../localization.md#what-needs-an-id)).

Personas cover the "derived actor" need (the same poses, and the same voice-file and backlog identity) without introducing a second actor for the writer to switch between.

**Grammar:**

```
actor_prop      ::= … | persona_prop
persona_prop    ::= "persona" NAME ("if" expr)? ":" EOL INDENT name_prop DEDENT     /* Q19 */
```

`if` is the bare keyword here, not `@if`, and `:` ends the expression.

**Guide text:**

> #### Personas: mystery characters and disguises
>
> A persona changes how a character is presented while something is true. Use one for a character the player hasn't met yet, or someone in disguise. The persona is set up once in the cast list:
>
> ```
> @actor rex:
>     name: Rex
>     poses: neutral, grumpy, surprised
>     persona stranger if not $met_rex:
>         name: ???
> ```
>
> In the story, always write the real character:
>
> ```
> rex: While $met_rex is false, this line shows "???" as the speaker.
> @set $met_rex = true
> rex: Now this line shows "Rex".
> ```
>
> You never switch names by hand. Whichever branch introduces Rex just sets `$met_rex`, and every line from then on uses his real name. The game can also change his look, for example to a silhouette, while he's "???".

### Terms

Terms are reusable, translatable pieces of wording. A term is declared once with whatever logic it needs, with `@term name = inline text`, then referenced by name anywhere inline text is allowed, as `{name}`. The main use is wording that depends on story state, such as player pronouns:

```
@enum pronouns: he, she, they
@var $pronouns: pronouns = they

@term they = {if $pronouns == he}he{elif $pronouns == she}she{else}they{/if}
@term them = {if $pronouns == he}him{elif $pronouns == she}her{else}them{/if}
@term their = {if $pronouns == he}his{elif $pronouns == she}her{else}their{/if}
@term are = {if $pronouns == they}are{else}is{/if}
@term s = {if $pronouns != they}s{/if}
```

```
mira: {They} {are} late again. Tell {them} I said hi.
mira: {They} walk{s} in like {they} own{s} the place.
```

With `$pronouns` set to `she`, those read: *"She is late again. Tell her I said hi."* and *"She walks in like she owns the place."*

- **Names are lowercase.** `{They}` refers to the term `they` and capitalizes the first letter of the result, using the current locale's casing rules. So one term covers both the start and the middle of a sentence.
- **A term's body is inline text,** limited to text, markup, interpolation, conditionals and other terms. It can't contain effects, pacing, icons or variations, so a term only ever produces wording. A term that refers to itself, directly or through other terms, is an error.
- **Terms are values, not effects.** A translation may use different terms or none at all ([localization design](../localization.md#what-a-translation-may-change)). Japanese often leaves pronouns out entirely.
- **Each locale translates the term bodies,** and can declare extra terms its grammar needs, such as Spanish adjective endings ([localization design](../localization.md#terms-in-translations)).
- **Terms work with any state,** not just enums. A game with free-form custom pronouns can build terms from string variables (`@term they = {$pronoun_subject}`).

**Grammar:**

```
declaration     ::= … | term_decl
term_decl       ::= "@term" NAME "=" inline_text EOL
point_body      ::= … | NAME call_args?
```

- `call_args` becomes optional in `point_body`. After the NAME, `(` means a call and `}` means a term.
- A capitalized reference (`{They}`) is an ordinary NAME, which the binder matches to its lowercase term.
- A term body ends in text, so like a text line it can't have a trailing comment. A term takes no tags, so a `#` followed by a letter in its body is an error, escaped as `\#`.
- What a term body may contain is a check.

**Guide text:**

> #### Reusable wording: terms
>
> Some wording depends on the story in many places, like the player's pronouns. Instead of writing the same condition every time, write it once as a **term** in the cast list, then use the term's name in curly braces.
>
> ```
> @term they = {if $pronouns == he}he{elif $pronouns == she}she{else}they{/if}
> @term them = {if $pronouns == he}him{elif $pronouns == she}her{else}them{/if}
> @term are = {if $pronouns == they}are{else}is{/if}
> @term s = {if $pronouns != they}s{/if}
> ```
>
> ```
> {They} {are} running late. Tell {them} this line uses terms.
> {They} walk{s} in, and this line still reads correctly for every choice.
> ```
>
> - **Capitalize the name to capitalize the word.** `{They}` gives "She" or "They" at the start of a sentence, and `{they}` gives "she" or "they" mid-sentence.
> - A term name is always the word as written for "they": `{they}`, `{them}`, `{their}`, `{are}`. That keeps lines readable even before they're filled in.
> - Terms can hold any wording that repeats, not just pronouns: a title the player chose, a nickname that changes partway through the story, and so on.
> - Translators get their own versions of each term, and can add terms their language needs. Your lines don't change.

### `speaker` parameter defaults

An `actor` parameter can default to `speaker`, meaning the speaker of the line the command appears in: `@command jolt(who: actor = speaker) inline` lets `{@jolt}` jolt whoever is talking. Using such a command where there's no speaker (narration, or on its own `@` line) without passing the argument is an error. Its first use is [voice barks](../localization.md#voice-barks).

**Grammar:**

```
constant        ::= … | "speaker"
```

v1 reserves `speaker`, so no v1 constant is spelled that way. Allowing it only as the default of an `actor` parameter is a check.

### Drafts

Draft content is written and tested alongside the real story, but isn't part of what ships.

```json
"drafts": ["drafts/**"]
```

```
== kitchen.new_scene #draft
```

- **A node is a draft** if its file matches a `drafts` glob in `pibbles.json`, or if its header has a `#draft` tag. Declarations in a draft file are drafts too. Paths in `pibbles.json` settings are relative to the story folder.
- **Development builds include drafts.** They're parsed, checked, playable in `pibbles play`, and startable by the host like any other node.
- **Release builds compile as if drafts didn't exist.** Anything that then fails to resolve is an ordinary error in `pibbles check --release`, for example a real node that jumps to a draft, or uses a variable only a draft declares. In development builds the same references get a warning (PIB3030), so the problem shows up long before release.
- **Drafts skip the production pipeline:** line IDs aren't required, `pibbles ids` leaves them alone, and they're excluded from localization extraction, recording scripts and release manifests.
- **Promoting a draft** means moving it out of the drafts path or removing its `#draft` tag. From then on it's held to the full rules, and `pibbles ids` gives it IDs.

The build profiles are in the [runtime design](../runtime.md#drafts).

**Grammar:** no change. `#draft` is a tag, and v1 reserves it.

**Guide text:**

> #### Drafts
>
> Work in progress can live right next to the real story without getting in anyone's way. A node is a draft if it's in a drafts folder (usually `story/drafts/`) or its header has a `#draft` tag:
>
> ```
> == kitchen.new_scene #draft
> This scene is still being written.
> ```
>
> - **Drafts are checked and playable** in the editor, `pibbles play` and development builds of the game, so you can test them with the real story.
> - **Drafts never ship.** Released builds leave them out completely.
> - **Drafts skip the paperwork:** no line IDs, no translation, no voice recording.
> - **If real content jumps into a draft,** Pibbles warns you, because that jump would break once drafts are left out.
> - **When a draft is ready,** move it out of the drafts folder or remove `#draft`. From then on it's treated like any other part of the story.

### Migrations

Release manifests record what each shipped build contained, so `pibbles check` can guarantee that every old save still has somewhere to go ([runtime design](../runtime.md#release-manifests-and-migrations)). Two language features give retired IDs that somewhere.

**`#was:` on lines.** For a retired line that needs no state fix, a `#was:` tag on the line to resume at is enough: `mira: The new wording. #id:q1w2e3 #was:k7qp2x`. `#was:$old_name` on a `@var` keeps old saves' values through a variable rename.

**Migration nodes** keep old saves working after an update removes or changes the place they point to. They're the escape hatch for the release checks:

```
== migrate.door_rewrite #migrates:k7qp2x,m3xw9a
@set $has_key = true
@resume h8ya3k

== migrate.cut_subplot #migrates:cellar.old_subplot,cellar.old_ending
@set $subplot_done = true
@resume c2nb7x
```

A `#migrates:` tag takes a comma-separated list of claims. A save pointing at a claimed ID runs the migration on load, instead of failing. Claims are resolved against the recorded releases, which know every shipped ID, the node it belonged to, and its order within that node:

| Claim | Covers |
| --- | --- |
| `k7qp2x` | That ID |
| `k7qp2x..m3xw9a` | Every shipped ID from the first to the second, inclusive, in the node's shipped order. Both must be from the same node. |
| `cellar.old_subplot` | Every ID the shipped node had. The claim also stands in for the node's name: if the host starts a removed node (for example from an `on_exit=` it saved), the migration runs instead. Node names and line IDs never coincide, so a one-word claim like `intro` always means one or the other. |
| any of the above + `@<version>` | Only saves made with that release (`k7qp2x@1.0`). This is the only way to claim IDs that still exist, for when an update adds or removes state changes before a line that shipped. |

- **Claims only apply to IDs that don't resolve otherwise.** A node or range claim never captures lines that still exist, or that a `#was:` alias already covers. So claiming a whole node is safe even if some of its lines were moved elsewhere.
- **The most specific claim wins:** a single ID, then a range, then a node. Two claims of the same specificity covering the same ID are an error.
- **Every entry must match something** in a recorded release. A mistyped ID or node name is an error, not a silent no-op.
- The body can do anything a node can, and must end with `@resume <id>` (continue at that line or choice), `@jump` or `@end`. `@resume` is only valid in migration nodes.
- Nothing else may jump to, call or start a migration node.
- Relative names work in `#migrates:` like anywhere else a node name does.

**Grammar:**

```
var_decl        ::= "@var" VARIABLE (":" type)? "=" constant TAG* EOL
flow_stmt       ::= … | "@resume" NAME EOL
claims          ::= claim ("," claim)*                                   /* a #migrates: value; Q20 */
claim           ::= NAME (".." NAME)? ("@" version)?                     /* Q20 */
```

- `#was:` on text lines and options needs no grammar change, since they already take tags. Allowing it there, with a line ID as its value, is a check. `@var` gains tags.
- A `#migrates:` value is parsed with the claim grammar after lexing, since v1's TAG value already runs to the next whitespace.
- `@resume` only in a migration node, and a range's two ends being line IDs from the same node, are checks.

**Guide text:**

> #### Updating a released game
>
> Once a version of the game has shipped, players have saves pointing at specific lines. If an update deletes or replaces a line, those saves need to know where to go instead. Pibbles won't let an update ship until every one of them does.
>
> - **Replacing a line:** put the old ID on the new line with `#was:`, and saves on the old line resume on the new one:
>
>   ```
>   mira: This is the new version of the line. #id:q1w2e3 #was:k7qp2x
>   ```
>
> - **Deleting a line** works the same way. Put its ID on the line where old saves should pick up.
> - **When old saves also need fixing,** for example because the deleted part set a variable that later lines depend on, write a migration node. It runs for those saves only, then continues where you say:
>
>   ```
>   == migrate.door_rewrite #migrates:k7qp2x
>   @set $has_key = true
>   @resume h8ya3k
>   ```
>
> - **One migration can cover many lines.** List IDs with commas (`k7qp2x,m3xw9a`), cover a stretch with a range (`k7qp2x..m3xw9a`), or name a whole node that was cut (`#migrates:cellar.old_subplot`). The editor can write this header for you: it offers a fix on the "nowhere to go" error that picks the shortest way to cover everything.
>
> `pibbles check` lists every old save point that has nowhere to go, and warns when an update adds or removes a variable change before a line that already shipped.

### Open questions

Each is decided when its extension is implemented, preferring to reject a form over giving it a fallback meaning, as the [v1 grammar](reference.md#appendix-grammar) does.

| Q | Production | Question | Options |
| --- | --- | --- | --- |
| 18 | `alternative_text` | Is a `\|` inside a construct nested in a variation (`{cycle}a{if $x}b\|c{/if}{/cycle}`) text, or an error? And a `\|` directly inside `{once}`, which has only one alternative? | Text, since a separator can't cross the nested construct's boundary; an error that suggests escaping with `\\|` |
| 19 | `persona_prop` | Can a persona override nothing? A persona that only changes host presentation, like sprites, has no `name:` to set. | The `:` and block become optional; the block stays required, and can be empty |
| 20 | `claim` | What is a `version`? | Runs to the next `,`; follows the `pibbles.json` version format |
| 21 | `variation` (`@shuffle`) | Where does each round's order come from? | A shuffle bag drawn from the state's random generator, saved per block; a permutation derived from the seed, the block's ID and the round number, as `{shuffle}` does, so nothing beyond the entry count is saved and a block never draws from the generator |

## With localization

What the language gains with the localization pipeline ([localization design](../localization.md)).

### Notes

`///` lines directly above a text line or option attach to it. Localization export and voice scripts include them, and the language server shows a declaration's notes in hovers.

```
/// Sarcastic. She's said this about every door so far.
mira (worried): Locked.{w} Of course it's locked.
```

**Grammar:** notes give the NOTE line class a meaning: `///`, then RAW. To keep the syntax LL(1), the parser attaches a note to the next TEXT or OPTION line as trivia, the way comments are dropped, instead of reading it as a grammar symbol. As a symbol, `note_line*` would start both `text_line` and `option`, and telling them apart would need lookahead past every note. A note that isn't directly above a text line or option is a check. Q2 decides which lines are notes.

**Guide text:**

> #### Notes for translators and voice actors
>
> A `///` line right above a line or option attaches a note to it. Translators and voice actors see it, and players don't.
>
> ```
> /// Said quietly, almost to herself.
> mira: This line has a note for whoever translates or records it.
> ```
>
> #### Writing for translation
>
> Translators can change the wording, move emphasis and adjust pauses however their language needs. They can't remove sounds, animations, pose changes, icons, or styles like `[clue]` that the game depends on. Pibbles checks this for every translated line. A few habits make their job easier:
>
> - Use conditional wording (`{if}`) instead of building sentences from pieces in separate lines.
> - Keep a whole thought in one line. Don't split one sentence across two lines.
> - Add a `///` note whenever tone, a pun or context isn't obvious from the line alone.

### `required` markup

`@markup clue required` means every translation must keep the markup ([localization design](../localization.md#what-a-translation-may-change)). Actor and persona display names become localized at the same time.

**Grammar:**

```
markup_decl     ::= "@markup" NAME ("(" params? ")")? "required"? EOL
```

`required` isn't reserved: it's a keyword only in this position, which v1 grammar doesn't have.

**Guide text:**

> - `required` on a style means translations must keep it, so changing how every clue looks is a one-line change for the game, and translations are required to keep it.

### Voice

Recorded lines, `#voice:` and `#unvoiced`, and barks are designed in the [localization design](../localization.md#voice). `#voice` and `#unvoiced` are reserved tags in v1.

**Guide text:**

> #### Voice
>
> - **Recorded lines play automatically.** If an actor recorded a line, the game finds the recording by the line's ID. You don't write anything.
> - **Voice barks** are short clips that match the mood of a line without reading it out: a sigh, a laugh, a "hmm". Put them in like any in-line command:
>
>   ```
>   mira (sad): {@bark sigh}This line starts with a sigh.
>   mira: This line has a thoughtful noise here...{@bark hmm} in the middle.
>   ```
>
>   A bark belongs to whoever is speaking the line. Changing a line's wording never affects its barks.
> - **Narration isn't recorded by default,** and neither is any character the project marks as silent. They still show normally.
> - **Recorded lines should avoid wording that changes** with the story, like terms or conditional text. Each version would need its own recording. Never put the player's typed name in a recorded line, because no one can record it.
