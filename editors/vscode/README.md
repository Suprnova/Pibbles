# Pibbles for VS Code

Syntax highlighting and snippets for `.pib` files, the stories written in [Pibbles](https://github.com/Suprnova/Pibbles).

- Headers, speakers and poses, `@` statements, options, `[markup]`, `{points}`, tags and comments are highlighted.
- Line IDs (`#id:k7qp2x`) are faded like comments, since they're for tools rather than for reading.
- Snippets cover node headers, `@if`, choices, `@sequence`, `@cycle`, `@once` and `@actor`.
- With the [Code Spell Checker](https://marketplace.visualstudio.com/items?itemName=streetsidesoftware.code-spell-checker) extension installed, `.pib` files are spell-checked, skipping everything but the text players see, with `story/words.txt` as the project's dictionary.

## Install

The extension isn't on the Marketplace. Download the `.vsix` from a [pre-release](https://github.com/Suprnova/Pibbles/releases), or build it, then:

```text
code --install-extension pibbles-<version>.vsix
```

## Build

Needs Node 22 or later.

```text
npm ci
npm test           compiles the grammar, runs the grammar tests, and checks every scope is tested
npm run package    builds the .vsix
```

The grammar is written in `syntaxes/pibbles.tmLanguage.yaml`. Each `.pib` file in `tests/` marks the scope a token should get on the comment lines under it, as [vscode-tmgrammar-test](https://github.com/PanAeon/vscode-tmgrammar-test) describes.
