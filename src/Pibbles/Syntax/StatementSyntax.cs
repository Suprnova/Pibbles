namespace Pibbles.Syntax;

/// <summary>A statement in a node's body or in a block.</summary>
public abstract record StatementSyntax : SyntaxNode;

/// <summary><c>@jump node</c>: continues at another node, leaving the call stack as it is.</summary>
/// <param name="Target">The node to continue at.</param>
public sealed record JumpStatementSyntax(NameSyntax Target) : StatementSyntax;

/// <summary><c>@call node</c>: runs another node, then comes back.</summary>
/// <param name="Target">The node to run.</param>
/// <param name="Tags">The tags after the target, such as its <c>#id</c>.</param>
public sealed record CallStatementSyntax(NameSyntax Target, IReadOnlyList<TagSyntax> Tags) : StatementSyntax;

/// <summary><c>@return</c>: returns from the current <c>@call</c>, or ends the dialogue at the top level.</summary>
public sealed record ReturnStatementSyntax : StatementSyntax;

/// <summary><c>@end</c>: ends the dialogue and clears the call stack.</summary>
public sealed record EndStatementSyntax : StatementSyntax;

/// <summary>A statement the parser doesn't read yet, and the statements in the block under it.</summary>
/// <param name="Body">The statements in the indented block under the line, if any.</param>
public sealed record UnparsedStatementSyntax(IReadOnlyList<StatementSyntax> Body) : StatementSyntax;
