# Syntax design

The front end turns `.pib` source into syntax trees and syntax diagnostics. It implements the lines, tokens and syntax layers of the [grammar](language/reference.md#appendix-grammar).

## Line classifier and parser

Parsing happens in two levels, following the line-oriented principle:

1. **Line classifier.** It splits the source into lines, measures indentation, emits INDENT and DEDENT, and classifies each line by its leading marker ([reference](language/reference.md#line-classification)). Indentation errors are reported here.
2. **Statement parser.** A hand-written recursive-descent parser for each line kind. Expressions use precedence climbing.
3. **Inline parser.** A separate small parser for inline text. It runs on text-line bodies and option text, and also on translation strings, where it's the same parser.

The statement and inline parsers read tokens from a lexer with three modes, code, inline and raw ([tokens](language/reference.md#tokens)). Whitespace between tokens is dropped, except that a `(` touching the name before it becomes its own token, CALL_OPEN. That keeps `has_item(x)` and `has_item (x)` apart while every decision stays one token deep.

Why hand-written, with no parser generator: the grammar is small, error recovery and messages matter more than grammar brevity, and the result is ordinary C# that any agent or contributor can step through.

**Error tolerance:** a line that fails to parse becomes an error node with one diagnostic, and parsing carries on with the next line. A bad indent is reported once, and the line is attached to the closest sensible block. Unclosed inline markup is reported at its opening bracket, and the span is treated as running to the end of the line.

## Syntax checks

The front end owns every check that needs only one file's text: indentation, malformed tokens, file and block structure, span nesting, escapes, where tags and `#id` may go, argument order, call adjacency, what option text may contain, and display names. These are the PIB1xxx codes ([diagnostics](diagnostics.md)). Anything that needs declarations or another file, such as whether a speaker is a declared actor, belongs to the [semantic passes](semantics.md#passes).

## Syntax tree

- Immutable `record` types. Each node carries a `TextSpan` (start and length in the file), and positions map to line and column through a per-file line map. The parts of a node that later stages report on or edit, such as a speaker's name, its pose and each tag, carry their own spans.
- The tree is not lossless: it keeps no trivia. Tools that edit source (inserting line IDs, quick fixes) edit text at node spans, which is enough. A lossless tree would only be needed for a formatter, and that's a stretch goal.
- Every edit reparses the whole file. Files are small, and whole-file parsing is fast enough for per-keystroke language-server updates ([performance](runtime.md#performance)). Incremental parsing is out of scope.

## Diagnostics

```csharp
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, SourceLocation Location, string Message, string? Label, string? Help);
public readonly record struct SourceLocation(string Path, TextSpan Span, LinePosition Start, LinePosition End);
```

- Codes are stable and grouped by stage: `PIB1xxx` syntax, `PIB2xxx` declarations and binding, `PIB3xxx` flow and content checks, `PIB4xxx` localization, `PIB5xxx` style ([semantics design](semantics.md)), `PIB6xxx` spelling ([tooling design](tooling.md#spell-checking)). The range also gives a code's `.editorconfig` category. Severities are error, warning, info and hint. Hints are for pure style: VS Code shows them as faint dots, and the CLI shows them only with `--style`.
- Every code has one descriptor in `DiagnosticCatalog`, a named static member such as `DiagnosticCatalog.MixedIndentation`: code, default severity, and templates for the message, the label and the help. `docs/diagnostics.md` documents each code. `.editorconfig` can override a code's severity, per code or per category, and per folder ([tooling design](tooling.md#configuration)).
- A code is registered in the same change as the check that reports it, so every registered code always has a fixture and an entry in `docs/diagnostics.md`.
- Templates are formatted with the invariant culture. Messages quote Pibbles source, which always writes `0.5` with a dot, and the same diagnostic must read the same on every machine.
- Positions are 0-based in the core and 1-based wherever a person reads them. A column counts UTF-16 code units, as .NET strings and the language server protocol do, so a tab is one column. A `SourceLocation` carries its line and column, so a consumer needs no source text to print it.

### How diagnostics read

Most people who read a diagnostic are writers, not programmers. Every message follows the [writing rules in the catalog](diagnostics.md#writing-messages): a plain headline, the source line with the problem marked, and a concrete fix.

```text
error[PIB1015]: This text comes after a tag, but tags go at the end of the line.
  --> story/kitchen.pib:12:11
   |
12 | mira: I'm #winning today.
   |           ^^^^^^^^ this starts a tag
   |
   = help: If "#winning" is part of what Mira says, put a backslash before the #:
           mira: I'm \#winning today.
```

The **message** is the headline, the **label** sits under the marked span, and the **help** says how to fix it. The label and help are optional. This layout is the CLI's default output ([tooling design](tooling.md#cli-pibbles)), and the language server shows the same message and help in hovers.
