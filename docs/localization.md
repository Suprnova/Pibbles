# Localization design

## Scope

Full-featured i18n (bidi, plural rules, units) would be overengineering for a VN scripting language. But skipping localization until later is the more expensive mistake. The foundation (stable line IDs, text kept apart from logic, and a strict rule for what a translation may change) is cheap to build now and very painful to retrofit into a finished script. So localization comes in three tiers:

| Tier | Contents | When |
| --- | --- | --- |
| Foundation | Line ID syntax, ID checks, the template model shared by source and translations | Phases 1–3 |
| Pipeline | `pibbles ids`, PO extract and update, translation validation, loading locales at runtime, fallback | Phase 5 |
| Advanced | Plural selection, bidi isolation of interpolated values, voice tooling | Stretch |

## Line IDs

```
mira (worried): Locked.{w} Of course it's locked. #id:k7qp2x
-> Use the key  @if $has_key #id:m3xw9a
```

### What needs an ID

Everything below applies to the real story. [Drafts](language/design.md#drafts) need no IDs and are never extracted for translation or recording.

Every text line and option, since those are what's shown, translated, voiced or saved at. Also every `@call`, because a save can be waiting inside the called node. Pose-only lines (`mira (sad):`) take no ID: they show nothing and complete at once, so a save never waits on one. Variation blocks also get an ID on their opener (`@once #id:b8k2qd`), which keys how many times the block has run, so it survives blocks being inserted, reordered or moved. Other translatable text gets an implicit ID from its declaration: actor and persona names (`actor.mira`, `actor.rex.stranger`) and term bodies (`term.they`). IDs must be unique across the story, and a duplicate is an error.

IDs are what everything else keys on:

| Keyed by line ID | Why it has to survive edits |
| --- | --- |
| Translations | A typo fix in the source mustn't orphan every translation of that line |
| Voice recordings | Same for the recorded audio |
| Saves: the position of a dialogue, chosen options, block entry counts, line show counts (with inline variations) | A patch mustn't lose a player's place, reset their progress or reshow `@once` options. Released builds check that every shipped ID still resolves ([runtime design](runtime.md#release-manifests-and-migrations)). |

### Who writes them

**The tooling, never the writer's memory.** `pibbles ids` adds missing IDs in place, touching only the end of each line. The language server does the same as a code action, and optionally on every save. A pre-commit hook running `pibbles ids` is an alternative for writers who work outside VS Code.

The writer's only rule: **leave IDs alone.** That applies when rewriting a line (the ID keeps pointing at "this line") and when moving it to another file (the ID travels with it). Deleting a line retires its ID. After a release, a retired ID needs a `#was:` alias or a migration, so old saves know where to go ([language design](language/design.md#migrations)). Two situations need a tool rather than a rule:

- **Copy and paste duplicates an ID.** The analyzer reports it, and a quick fix gives the new copy a fresh ID.
- **Rewrites make downstream assets stale.** Keeping the ID after a rewrite is right, because it's the same line in the story. The assets made from the old text are what's out of date. Translations detect this through their `msgid` ([below](#stale-and-missing-translations)), and voice recordings through the recording manifest ([Voice](#recording-manifest-and-staleness-stretch)).

A generated ID is a random lowercase letter followed by five random lowercase letters or digits (`[a-z][a-z0-9]{5}`). A hand-written ID is also accepted (`#id:intro_greeting`), for the rare line the host refers to by name. Every ID has the same shape: a lowercase letter, then lowercase letters, digits and `_` ([reference](language/reference.md#lexical-basics)).

### When they're required

A missing ID is a warning (PIB3010). It never stops the story from compiling, so a line written a minute ago can be playtested before `pibbles ids` runs. CI runs `pibbles check --release --warnaserror`, so no line reaches a release without an ID.

Until it gets an ID, a line has a **fallback identity**: an internal key the compiler assigns, which no source file can spell. It lets the runtime track the line's option state and position while the game runs, but it's never written to a snapshot, because it can't survive an edit. When a snapshot is taken:

- State keyed by a fallback (chosen options, `@once` removal, block entry counts) is left out.
- A dialogue waiting on a line or choice without an ID is saved as having no dialogue in progress.

Both are listed in the snapshot's report, which suggests running `pibbles ids`. This only ever affects development saves, and it can never send a save to the wrong line.

### Keeping them out of the way

The IDs live in the file, but writers don't have to look at them. The VS Code extension fades `#id:` tags, or hides them except on the line under the cursor (a setting). It works from the TextMate grammar alone, so it's available from Phase 1.

### Alternatives considered

| Approach | Why not |
| --- | --- |
| **IDs derived from content** (a hash of the text, as Ren'Py does) | Any edit, even a typo fix, changes the ID. Localization can work around that with fuzzy matching, as gettext does, but saves and voice recordings can't: a patch would reset `@once` options and orphan recordings. |
| **IDs derived from position** (node plus line number) | Inserting a line renumbers everything after it, so saves and translations would silently attach to the wrong line. |
| **A sidecar file** mapping lines to IDs, maintained by tooling | The tool has to *guess* which line is which after every edit, by matching old text against new, and a guess that's wrong fails silently. The sidecar is a generated file that conflicts on every parallel edit to the same scene, and conflicts in random IDs can't be resolved by regenerating. Edits made without the tool running drift out of sync. |

In-file IDs are explicit, travel with the line through moves and copies, show up in diffs, and never need a heuristic. They're the same approach Yarn Spinner uses. The price is visual noise, which the editor handles.

## Workflow

```
pibbles.json                { "sourceLocale": "en", "localization": { "folder": "loc", "locales": ["es", "ja"] } }
story/
  loc/
    template.pot            generated; never edited by hand
    es.po
    ja.po
```

1. Writers write in the source locale and run `pibbles ids`.
2. `pibbles loc update` regenerates `template.pot` and merges it into each `<locale>.po`. New lines appear untranslated. Lines whose source text changed are marked fuzzy. Removed lines become obsolete entries.
3. Translators work in standard PO tools (Poedit, Weblate, Crowdin).
4. `pibbles check` validates every locale along with the source. CI runs it.
5. At runtime, the host sets `runner.Locale`, and Pibbles looks up each line in that locale.

A PO entry:

```
#. Sarcastic. She's said this about every door so far.
#. Speaker: Mira (worried)
#: kitchen.pib:12
msgctxt "k7qp2x"
msgid "Locked.{w} Of course it's locked."
msgstr "Cerrada.{w 0.3} Cómo no, está cerrada."
```

- `msgctxt` holds the line ID. `msgid` holds the source inline text, with the speaker prefix, tags and modifiers stripped. Notes (`///`), the speaker and the source location become extracted comments.
- The core reads and writes PO itself. The format is small, and the core stays free of dependencies.

**Why PO:** it's the format translation tools and services support best. It carries context, notes and fuzzy state. Godot also uses PO for UI strings, so the game's UI text and story text can share one translation pipeline and one vendor.

**Why story text doesn't go through Godot's `TranslationServer`:** Pibbles has to parse and validate translated markup, and render it against story state. Routing text through `tr()` would bypass all of that, and would tie the core to Godot. The adapter only keeps the locale in sync.

## What a translation may change

Translators need freedom. Word order changes, emphasis moves, and pauses land somewhere else. But they must not be able to break the game. Following the principle *effects are sacred, information is protected, presentation is negotiable*, the validator compares the source and translated templates by category:

| Category | Elements | Rule | On violation |
| --- | --- | --- | --- |
| Effects | `{@command}`, pose changes, `{auto}` | Same multiset (same elements with the same arguments). Position may change. | Error |
| Information | `{icon}`, `required` markup | Same multiset. Position and extent may change. | Error |
| Values | `{$var}`, `{fn()}` interpolations | Same multiset | Warning |
| Logic | `{if}`, variations, `{plural}`, term references | Free to add, remove or restructure. Must bind: only declared variables and functions, correct types. | Normal binding errors |
| Presentation | Style markup, `[speed]`, `{w}`, `{p}`, `{br}` | Free | — |

Some consequences:

- **A translation can add logic the source doesn't need.** English "you're tired" can become Spanish `{if $player_gender == female}cansada{else}cansado{/if}` without touching the source. The reverse is allowed too.
- **Term references are free.** A language without pronoun-verb agreement just drops `{are}`, and one with gendered adjectives uses its own terms.
- **Inline variations are free,** because selection depends on the line's show count, not on each variation's state ([language design](language/design.md#inline-variations)). A translation with four alternatives where the source has three stays deterministic and save-compatible.
- **Effects in a conditional branch count as present** if they appear in any branch. The validator compares the set of effects that *could* fire, taken across all branches.
- **Spans must still nest properly** and close within the line. The inline parser that checks source text checks translations too.

## Terms in translations

[Terms](language/design.md#terms) are localized like lines, but each locale has more freedom with them:

- **Every term body is a PO entry** (`msgctxt "term.they"`), translated with the locale's own logic. The Spanish body for `they` covers *él*, *ella* and *elle*.
- **A locale can declare extra terms** in `loc/<locale>.pib`, a declarations-only file that may contain only `@term`. Those terms are visible only in that locale's translations. For example, Spanish can add an adjective-ending term:

  ```
  // loc/es.pib
  @term o = {if $pronouns == he}o{elif $pronouns == she}a{else}e{/if}
  ```

  ```
  msgid "{They} {are} tired."
  msgstr "{Elle} está cansad{o}."
  ```

- Locale terms follow the same rules as source terms. They can only read declared variables and functions, and they can't contain effects.

## Stale and missing translations

- **Stale:** the entry's `msgid` no longer matches the current source text, because the source changed after translation. Changes that only touch pacing, whitespace or style markup don't count: the translation carries over unchanged, since translators control those themselves. `pibbles loc update` marks it fuzzy, and `pibbles check` warns.
- **At runtime,** a missing or fuzzy entry falls back to the next locale in the chain. The chain runs from the specific locale to its language to the source (`pt-BR` → `pt` → `en`). An invalid entry (one with validation errors) also falls back, and is logged, so a broken translation shows the source text instead of breaking the game.

## Voice

Pibbles supports three kinds of voice, and only the first is tied to a line's words:

| Kind | Example | How it's written | Tracked for staleness |
| --- | --- | --- | --- |
| **Recorded lines** | The line read out in full | Nothing. The recording is found by line ID. | Yes |
| **Voice barks** | A sigh, a "hmm", a laugh, a catchphrase grunt that fits the line's mood | A declared inline command: `{@bark sigh}` | No |
| **Voice blips** | Typewriter beeps in each character's voice | Nothing. The host picks them by speaker. | No |

### Recorded lines

- Recordings are keyed by locale and line ID (for example `voice/<locale>/<id>.ogg`). The host owns the path convention, and Pibbles supplies the ID. At runtime, a line plays its recording if one exists. There's no separate "is this line voiced" switch.
- `#voice:<line ID>` makes a line reuse another line's recording, for identical lines like "Yes." that appear in many places. One occurrence is recorded, and the others point at it. The target must exist and should have the same speaker, and recording scripts and staleness checks treat the line as sharing the target's text. Recordings stay keyed purely by line ID.
- **What's expected to be recorded** matters only to the tooling (recording scripts, missing-recording reports). By default that's every actor's lines. Narration and specific actors (a silent protagonist) are excluded through `pibbles.json`: `"voice": { "narration": false, "unvoiced": ["sam"] }`. `#unvoiced` excludes a single line.
- **Wording that depends on state** can't be covered by one recording. A recorded line that uses terms or conditionals needs one recording per variant. The recording script lists each variant, and the variant is part of the clip key. Interpolating free text into a recorded line (`{$player_name}`) is an analyzer warning, since nobody can record it.

### Voice barks

Barks are ordinary declared commands, marked `inline` so they can sit at a point in a line:

```
@enum bark: sigh, hmm, laugh, gasp
@command bark(kind: bark, who: actor = speaker) inline
```

```
mira (sad): {@bark sigh}I suppose that's that.
mira: Well...{@bark hmm} maybe not.
```

- `who` defaults to [`speaker`](language/design.md#speaker-parameter-defaults), so a bark belongs to whoever is talking unless it names someone else.
- The host plays barks on its voice bus. They follow the voice volume setting and the dub language, and resolve to a clip per actor. Mechanically they're the same as sound effects. They're a separate command because the game treats them differently.
- Rewording a line never makes a bark stale, because a bark was never tied to the words. Translations must keep barks, since they're effects, but can move them ([above](#what-a-translation-may-change)).

### Recording manifest and staleness (stretch)

A manifest records, for each recording, the line text it was made from. `pibbles check` compares that with the current text:

- **Changes that don't affect speech are ignored:** markup, pacing, inline commands, punctuation, capitalization and whitespace.
- **Small word-level edits,** like a typo fix, show as information, with the old and new wording: *"Recording for k7qp2x may be out of date: 'teh' → 'the'."*
- **Real rewrites** are a warning.
- **Either way, one confirmation clears it for good.** `pibbles voice accept <id>`, or the matching language-server quick fix, records the current text as matching the recording.

## Text direction, plurals and formatting

- **Bidi:** Pibbles keeps text, spans and markers in logical order, and never reorders anything. Godot's advanced text server does the shaping and visual reordering. **Stretch:** in right-to-left locales, the renderer wraps interpolated values in first-strong isolates (U+2068…U+2069) so a Latin player name doesn't scramble an Arabic sentence.
- **Numbers:** interpolated numbers are formatted with the locale's `CultureInfo`. Integral values print without decimals. If a host runs without culture data, formatting falls back to the invariant culture ([architecture](architecture.md#target-frameworks)).
- **Plurals (stretch):** `{plural $count}{one}a key{other}{$count} keys{/plural}`, with case keys from the CLDR categories (`zero one two few many other`) plus exact matches (`{=0}`). .NET has no plural-rules API, so this needs a small CLDR-derived rules table for the shipped locales. Until then, `{if $count == 1}` works for English-like languages, and translators can use their own conditionals. v1 doesn't reserve the plural words, so a story may already use `plural` or a case name as a function, term or actor name ([known exception](language/reference.md#extension-compatibility)). The final syntax is chosen with that in mind.
- **Gender, pronouns and similar agreement:** [terms](language/design.md#terms) built from `{if}` on story variables, with extra terms per locale as needed ([above](#terms-in-translations)).
- **Dates, units, currencies:** out of scope. If the story needs them, a host function returns localized text.
