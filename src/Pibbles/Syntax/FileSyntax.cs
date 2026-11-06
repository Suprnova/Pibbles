namespace Pibbles.Syntax;

/// <summary>The root of a file's syntax tree: an optional prefix, then the file's declarations and nodes, in source order.</summary>
/// <param name="Prefix">The file's <c>@prefix</c> line, or <see langword="null"/>.</param>
/// <param name="Members">The file's declarations and nodes, each a <see cref="DeclarationSyntax"/> or a <see cref="NodeSyntax"/>, in source order.</param>
public sealed record FileSyntax(PrefixSyntax? Prefix, IReadOnlyList<SyntaxNode> Members) : SyntaxNode
{
    /// <summary>The file's declarations, in source order.</summary>
    public IReadOnlyList<DeclarationSyntax> Declarations { get; } = [.. Members.OfType<DeclarationSyntax>()];

    /// <summary>The file's nodes, in source order.</summary>
    public IReadOnlyList<NodeSyntax> Nodes { get; } = [.. Members.OfType<NodeSyntax>()];
}

/// <summary>A <c>@prefix</c> line, which makes names starting with a dot relative to <see cref="Name"/>.</summary>
/// <param name="Name">The group name the file's relative node names are under.</param>
public sealed record PrefixSyntax(NameSyntax Name) : SyntaxNode;

/// <summary>A node: a header (<c>== name</c>) and every statement up to the next header or the end of the file.</summary>
/// <param name="Name">The node's name, full or relative.</param>
/// <param name="Aliases">The node's former names, from its <c>#was:</c> tags.</param>
/// <param name="Body">The node's statements.</param>
public sealed record NodeSyntax(NameSyntax Name, IReadOnlyList<NameSyntax> Aliases, IReadOnlyList<StatementSyntax> Body) : SyntaxNode;
