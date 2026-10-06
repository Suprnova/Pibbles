using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// What the analysis passes share: the file being analyzed, reporting in it, recording the names in it that refer to
/// symbols, and expanding its relative node names.
/// </summary>
internal abstract class AnalysisPass(List<Diagnostic> diagnostics, ReferenceIndex references)
{
    /// <summary>The file being analyzed.</summary>
    protected SyntaxTree Tree { get; set; } = null!;

    protected string TextOf(TextSpan span) => Tree.Source.Text.Substring(span.Start, span.Length);

    /// <summary>The part of a node on its first line, such as a block opener without its block.</summary>
    protected TextSpan FirstLine(SyntaxNode node)
    {
        TextSpan line = Tree.Source.GetLineSpan(Tree.Source.GetLinePosition(node.Span.Start).Line);
        return new(node.Span.Start, Math.Min(node.Span.End, line.End) - node.Span.Start);
    }

    /// <summary>
    /// Expands a node name with its file's prefix, or returns <see langword="null"/> if it can't be: the name is
    /// missing, or it's relative in a file with no prefix, which is reported.
    /// </summary>
    protected string? FullName(string written, TextSpan span)
    {
        if (written.Length == 0)
            return null;

        if (!written.StartsWith('.'))
            return written;

        if (Tree.Root.Prefix is not { } prefix)
        {
            Report(DiagnosticCatalog.RelativeWithoutPrefix, span, written);
            return null;
        }

        return prefix.Name.IsMissing ? null : prefix.Name.Text.TrimStart('.') + written;
    }

    /// <summary>Says where <paramref name="location"/> is, from the file being analyzed: <c>on line 3</c>, or <c>in story/cast.pib on line 3</c>.</summary>
    protected string Where(SourceLocation location) =>
        location.Path == Tree.Source.Path ? $"on line {location.Start.Line + 1}" : $"in {location.Path} on line {location.Start.Line + 1}";

    /// <summary>Records that the name at <paramref name="span"/> declares <paramref name="symbol"/>.</summary>
    protected void Declares(TextSpan span, Symbol symbol) => references.Add(Tree.Source.GetLocation(span), symbol, isDeclaration: true);

    /// <summary>Records that the name at <paramref name="span"/> refers to <paramref name="symbol"/>.</summary>
    protected void Refers(TextSpan span, Symbol symbol) => references.Add(Tree.Source.GetLocation(span), symbol, isDeclaration: false);

    protected void Report(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments) =>
        ReportAt(descriptor, Tree.Source.GetLocation(span), arguments);

    /// <summary>Reports a diagnostic at a location in any file, for checks that look across the story.</summary>
    protected void ReportAt(DiagnosticDescriptor descriptor, SourceLocation location, params object?[] arguments) =>
        diagnostics.Add(descriptor.Create(location, arguments));

    /// <summary>
    /// Reports a diagnostic whose help only has something to say sometimes, such as a suggestion. Its last argument
    /// fills the help, and the help is left out when that argument is <see langword="null"/>.
    /// </summary>
    protected void ReportWithOptionalHelp(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments)
    {
        Diagnostic diagnostic = descriptor.Create(Tree.Source.GetLocation(span), arguments);
        diagnostics.Add(arguments[^1] is null ? diagnostic with { Help = null } : diagnostic);
    }
}
