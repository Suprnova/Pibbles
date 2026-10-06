using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Checks what runs in each block: statements that never run, because one above them always leaves the block, and
/// options with no text.
/// </summary>
/// <remarks>
/// A statement always leaves when it's <c>@jump</c>, <c>@end</c> or <c>@return</c>, or an <c>@if</c> with an
/// <c>@else</c> whose every branch always leaves. That's decided from the statements alone: conditions are never
/// evaluated, so <c>@if false</c> is treated like any other condition. Choices and variations never count, since a
/// choice is skipped when no option is available, and a variation's block can be skipped too.
/// </remarks>
internal sealed class FlowChecks(List<Diagnostic> diagnostics) : AnalysisPass(diagnostics, new ReferenceIndex())
{
    public static void Run(IReadOnlyList<SyntaxTree> trees, List<Diagnostic> diagnostics)
    {
        var checks = new FlowChecks(diagnostics);
        foreach (SyntaxTree tree in trees)
        {
            checks.Tree = tree;
            foreach (NodeSyntax node in tree.Root.Nodes)
                checks.CheckBlock(node.Body);
        }
    }

    private void CheckBlock(IReadOnlyList<StatementSyntax> block)
    {
        int leaves = block.ToList().FindIndex(AlwaysLeaves);
        if (leaves >= 0 && leaves + 1 < block.Count)
            ReportNeverRuns(block[leaves + 1], block[leaves]);

        foreach (StatementSyntax statement in block)
        {
            foreach (IReadOnlyList<StatementSyntax> inner in SyntaxWalk.BlocksOf(statement))
                CheckBlock(inner);

            if (statement is ChoiceSyntax choice)
            {
                foreach (OptionSyntax option in choice.Options.Where(option => option.Text.Count == 0))
                    Report(DiagnosticCatalog.EmptyOption, new(option.Span.Start, 2));
            }
        }
    }

    private void ReportNeverRuns(StatementSyntax statement, StatementSyntax leaver)
    {
        (string because, string keyword) = leaver switch
        {
            JumpStatementSyntax => ("of the `@jump` above it", "@jump"),
            EndStatementSyntax => ("of the `@end` above it", "@end"),
            ReturnStatementSyntax => ("of the `@return` above it", "@return"),
            _ => ("every branch of the `@if` above it ends with `@jump`, `@end` or `@return`", "@if"),
        };

        Report(DiagnosticCatalog.NeverRuns, FirstLine(statement), because, keyword);
    }

    private static bool AlwaysLeaves(StatementSyntax statement) => statement switch
    {
        JumpStatementSyntax or EndStatementSyntax or ReturnStatementSyntax => true,
        IfStatementSyntax { Else: { } @else } @if => Leaves(@if.Body) && @if.ElseIfs.All(elseIf => Leaves(elseIf.Body)) && Leaves(@else.Body),
        _ => false,
    };

    private static bool Leaves(IReadOnlyList<StatementSyntax> block) => block.Any(AlwaysLeaves);
}
