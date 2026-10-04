namespace Pibbles.Syntax;

/// <summary>A node of a file's syntax tree.</summary>
/// <remarks>
/// Nodes are immutable, and keep no comments or whitespace. A node's <see cref="Span"/> covers its own text and the
/// text of every node under it, including a statement's indented block.
/// </remarks>
public abstract record SyntaxNode
{
    /// <summary>The text the node covers.</summary>
    public required TextSpan Span { get; init; }
}

/// <summary>A name: an identifier, or identifiers joined by dots, as in <c>kitchen.door</c>.</summary>
/// <remarks>
/// A name starting with a dot (<c>.door</c>) is relative to the file's <see cref="PrefixSyntax"/>. A name the source
/// leaves out, after a problem that has already been reported, has empty <see cref="Text"/> and a zero-length span.
/// </remarks>
/// <param name="Text">The name as written, such as <c>kitchen.door</c> or <c>.door</c>.</param>
public sealed record NameSyntax(string Text) : SyntaxNode
{
    /// <summary>Whether the source leaves the name out.</summary>
    public bool IsMissing => Text.Length == 0;
}

/// <summary>A tag such as <c>#thought</c> or <c>#box:phone</c>.</summary>
/// <param name="Name">The tag's name, without the <c>#</c>.</param>
/// <param name="Value">The text after the <c>:</c>, which may be empty, or <see langword="null"/> when the tag has no <c>:</c>.</param>
public sealed record TagSyntax(string Name, string? Value) : SyntaxNode;
