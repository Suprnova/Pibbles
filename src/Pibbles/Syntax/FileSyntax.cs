namespace Pibbles.Syntax;

/// <summary>The root of a file's syntax tree: an optional prefix, then declarations, then nodes.</summary>
/// <param name="Prefix">The file's <c>@prefix</c> line, or <see langword="null"/>.</param>
/// <param name="Declarations">The declarations before the file's first node.</param>
/// <param name="Nodes">The file's nodes, in order.</param>
public sealed record FileSyntax(PrefixSyntax? Prefix, IReadOnlyList<DeclarationSyntax> Declarations, IReadOnlyList<NodeSyntax> Nodes) : SyntaxNode;

/// <summary>A <c>@prefix</c> line, which makes names starting with a dot relative to <see cref="Name"/>.</summary>
/// <param name="Name">The group name the file's relative node names are under.</param>
public sealed record PrefixSyntax(NameSyntax Name) : SyntaxNode;

/// <summary>A node: a header (<c>== name</c>) and every statement up to the next header or the end of the file.</summary>
/// <param name="Name">The node's name, full or relative.</param>
/// <param name="Aliases">The node's former names, from its <c>#was:</c> tags.</param>
/// <param name="Body">The node's statements.</param>
public sealed record NodeSyntax(NameSyntax Name, IReadOnlyList<NameSyntax> Aliases, IReadOnlyList<StatementSyntax> Body) : SyntaxNode;

/// <summary>A declaration: part of the contract between the story and the host.</summary>
public abstract record DeclarationSyntax : SyntaxNode;

/// <summary>A declaration the parser doesn't read yet. Its line and any block under it are skipped.</summary>
public sealed record UnparsedDeclarationSyntax : DeclarationSyntax;
