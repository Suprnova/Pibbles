using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>The lines layer's output: the token stream the parser reads, and the problems found on the way.</summary>
internal sealed record ClassifiedLines(IReadOnlyList<LineToken> Tokens, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// Splits a source file into lines, classifies each one by how it starts, and turns indentation into
/// indent and dedent tokens, following the lines layer of the grammar in <c>docs/language/reference.md</c>.
/// </summary>
/// <remarks>
/// Blank, comment and note lines don't take part in indentation, and don't appear in the token stream.
/// </remarks>
internal sealed class LineClassifier
{
    private const char ByteOrderMark = '\uFEFF';

    private readonly SourceText source;
    private readonly List<LineToken> tokens = [];
    private readonly List<Diagnostic> diagnostics = [];
    private readonly Stack<int> widths = new([0]);
    private char? indentCharacter;

    private LineClassifier(SourceText source) => this.source = source;

    public static ClassifiedLines Classify(SourceText source) => new LineClassifier(source).Run();

    private ClassifiedLines Run()
    {
        for (int number = 0; number < source.LineCount; number++)
            Read(number);

        var end = new SourceLine(source.LineCount - 1, LineKind.EndOfFile, new(source.Text.Length, 0));
        for (; widths.Count > 1; widths.Pop())
            tokens.Add(new(LineTokenKind.Dedent, end));

        tokens.Add(new(LineTokenKind.Line, end));
        return new(tokens, diagnostics);
    }

    private void Read(int number)
    {
        TextSpan span = source.GetLineSpan(number);
        int start = number == 0 && source.Text.AsSpan(span.Start, span.Length) is [ByteOrderMark, ..] ? span.Start + 1 : span.Start;
        ReadOnlySpan<char> text = source.Text.AsSpan(start, span.End - start);

        ReadOnlySpan<char> content = text.TrimStart(" \t");
        var indentation = new TextSpan(start, text.Length - content.Length);
        var line = new SourceLine(number, KindOf(content), new(indentation.End, content.Length));

        if (line.Kind is LineKind.Note)
            Report(DiagnosticCatalog.UnsupportedNote, new(indentation.End, 3));

        if (line.Kind is LineKind.Blank or LineKind.Comment or LineKind.Note)
            return;

        Indent(line, indentation);
        tokens.Add(new(LineTokenKind.Line, line));
    }

    /// <summary>Classifies a line by how its content starts. A bare <c>-</c> is an alternative marker, like <c>- </c> followed by text.</summary>
    internal static LineKind KindOf(ReadOnlySpan<char> content) => content switch
    {
        [] => LineKind.Blank,
        ['/', '/', '/'] or ['/', '/', '/', not '/', ..] => LineKind.Note,
        ['/', '/', ..] => LineKind.Comment,
        ['=', '=', ..] => LineKind.Header,
        ['@', ..] => LineKind.At,
        ['-', '>', ..] => LineKind.Option,
        ['-'] or ['-', ' ' or '\t', ..] => LineKind.Dash,
        _ => LineKind.Text,
    };

    /// <summary>
    /// Emits the indents or dedents before a line. A line that lines up with no block joins the innermost block it fits in.
    /// A line that mixes tabs and spaces still indents by its width, so a region pasted with the other character keeps its
    /// structure, and only reports that it mixes them.
    /// </summary>
    private void Indent(SourceLine line, TextSpan indentation)
    {
        bool mixed = HasMixedIndentation(indentation);

        if (indentation.Length > widths.Peek())
        {
            widths.Push(indentation.Length);
            tokens.Add(new(LineTokenKind.Indent, line));
            return;
        }

        for (; indentation.Length < widths.Peek(); widths.Pop())
            tokens.Add(new(LineTokenKind.Dedent, line));

        if (indentation.Length != widths.Peek() && !mixed)
            Report(DiagnosticCatalog.InconsistentIndentation, indentation);
    }

    /// <summary>Reports a line that indents differently from the file. The first indented line decides how the file indents.</summary>
    private bool HasMixedIndentation(TextSpan indentation)
    {
        ReadOnlySpan<char> text = source.Text.AsSpan(indentation.Start, indentation.Length);
        if (text.IsEmpty)
            return false;

        indentCharacter ??= text[0];
        if (!text.ContainsAnyExcept(indentCharacter.Value))
            return false;

        string used = text.ContainsAnyExcept('\t') ? text.ContainsAnyExcept(' ') ? "tabs and spaces" : "spaces" : "tabs";
        Report(DiagnosticCatalog.MixedIndentation, indentation, used, indentCharacter is '\t' ? "tabs" : "spaces");
        return true;
    }

    private void Report(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments) =>
        diagnostics.Add(descriptor.Create(source.GetLocation(span), arguments));
}
