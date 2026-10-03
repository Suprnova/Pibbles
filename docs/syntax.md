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
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, SourceLocation Location, string Message);
```

- Codes are stable and grouped by stage: `PIB1xxx` syntax, `PIB2xxx` declarations and binding, `PIB3xxx` flow and content checks, `PIB4xxx` localization, `PIB5xxx` style ([semantics design](semantics.md)), `PIB6xxx` spelling ([tooling design](tooling.md#spell-checking)). Severities are error, warning, info and hint. Hints are for pure style: VS Code shows them as faint dots, and the CLI shows them only with `--style`.
- Every code has one entry in the diagnostic catalog: code, default severity, message template and a short explanation. `docs/diagnostics.md` documents each code. `.editorconfig` can override a code's severity, per code or per category, and per folder ([tooling design](tooling.md#configuration)).
- Messages say what is wrong and, where possible, how to fix it: *"Unknown actor 'Note'. If this line is narration, escape the colon: `Note\:`."*
