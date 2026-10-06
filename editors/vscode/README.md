# Pibbles for VS Code

Syntax highlighting, snippets and editing aids for `.pib` files, the stories written in [Pibbles](https://github.com/Suprnova/Pibbles).

- Headers, speakers and poses, `@` statements, options, `[markup]`, `{points}`, tags and comments are highlighted.
- Line IDs (`#id:k7qp2x`) are faded like comments, since they're for tools rather than for reading.
- Snippets cover node headers, `@if`, choices, `@sequence`, `@cycle`, `@once` and `@actor`.
- The cursor steps around line IDs: **End**, **Shift+End** and **Right** stop before a line's trailing `#id:`, a click past the end of a line lands before it, and Enter there keeps the ID on its line. **Ctrl+Alt+I** (**Cmd+Alt+I** on a Mac) selects the ID for the rare edit. The `pibbles.lineIds.skipWithCursor` setting turns this off.
- A variation's alternatives work like a Markdown list: Enter at the end of one starts the next, Enter on an empty `- ` leaves the variation, Tab on an empty `- ` turns it into a continuation of the alternative above, and typing `- ` on a continuation moves it out to the alternatives.
- Typing `[/` inside a span closes it: `[wave]Ominously.[/` becomes `[wave]Ominously.[/wave]`.
- With the [Code Spell Checker](https://marketplace.visualstudio.com/items?itemName=streetsidesoftware.code-spell-checker) extension installed, `.pib` files are spell-checked, skipping everything but the text players see, with `story/words.txt` as the project's dictionary.

## Install

The extension isn't on the Marketplace. Download the `.vsix` from a [release](https://github.com/Suprnova/Pibbles/releases), or build it, then:

```text
code --install-extension pibbles-<version>.vsix
```

## Build

Needs Node 22.18 or later, which runs the TypeScript tests directly.

```text
npm ci
npm test           compiles the extension and the grammar, runs the unit and grammar tests, and checks every scope is tested
npm run package    builds the .vsix
```

The editing aids are TypeScript in `src/`: `extension.ts` connects them to VS Code, and the logic lives in modules that don't import it, tested with `node --test` in `tests/unit/`. The grammar is written in `syntaxes/pibbles.tmLanguage.yaml`. Each `.pib` file in `tests/` marks the scope a token should get on the comment lines under it, as [vscode-tmgrammar-test](https://github.com/PanAeon/vscode-tmgrammar-test) describes.
