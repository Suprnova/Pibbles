# Diagnostics

The catalog of every diagnostic code. Each code has one entry here and one in `DiagnosticCatalog`, which the code registers it in. As a code is implemented, its entry here gains its meaning, an example that triggers it, and how to fix it. The same text shows up in language-server hovers.

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

## Codes

| Code | Default | Example message |
| --- | --- | --- |
| PIB1001 | Error | Mixed tabs and spaces in indentation |
| PIB1010 | Error | Unclosed markup `[clue]` |
| PIB1011 | Error | Markup closed out of order: expected `[/i]`, found `[/b]` |
| PIB1020 | Error | `@prefix` must come before everything else in the file except comments |
| PIB1030 | Error | `@else` has no `@if` directly above it at the same indentation |
| PIB2001 | Error | Unknown actor `Note`. If this is narration, escape the colon: `Note\:` |
| PIB2002 | Error | Actor `mira` has no pose `smirk`. Did you mean `smug`? |
| PIB2003 | Error | `(to Rex)` isn't a pose: a pose is a single name. For how a line is said, use a `//` comment. If this is narration, escape the colon: `mira (to Rex)\:` |
| PIB2004 | Warning | Add a space after `mira:`, or escape the colon if this is narration: `mira\:Hi` |
| PIB2010 | Error | Unknown command `@shak`. Did you mean `@shake_screen`? |
| PIB2011 | Error | `@show` expects a `position` for `at`, found `number` |
| PIB2012 | Error | `@give_item` can't be used inside a line because it isn't declared `inline`. Put it on its own `@` line. |
| PIB2020 | Error | Unknown node `kitchen.dor` |
| PIB2021 | Error | `.leave` is relative, but this file has no `@prefix` |
| PIB2030 | Error | Unknown variable `$has_kye` |
| PIB2045 | Error | Unknown tag `#thougth`. Did you mean `#thought`? If this is text, escape it: `\#thougth` |
| PIB2046 | Error | `#thought` takes no value. / `#box` needs a value: `#box:<string>` |
| PIB2047 | Error | `kitchen.front_door` is an old name of `kitchen.door`. Use the current name. |
| PIB3001 | Warning | Unreachable statement after `@jump` |
| PIB3010 | Warning | Line has no `#id`. Run `pibbles ids` to add one. |
| PIB3011 | Error | Duplicate line ID `k7qp2x` (also used at rooms/cellar.pib:40) |

The style rules, PIB5001 to PIB5040, are documented with examples in the [semantics design](semantics.md#style-rules).

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
