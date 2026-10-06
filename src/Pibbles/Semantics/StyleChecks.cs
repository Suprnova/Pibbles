using Pibbles.Configuration;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// The style rules (PIB5xxx): scripts that work correctly, but could be clearer, easier to translate or easier to
/// maintain. <c>docs/semantics.md</c> describes each rule. Thresholds come from each file's settings.
/// </summary>
internal sealed partial class StyleChecks(SymbolTable symbols, ReferenceIndex references, CompilationOptions options, List<Diagnostic> diagnostics)
    : AnalysisPass(diagnostics, new ReferenceIndex())
{
    private FileSettings Settings => SettingsOf(Tree);

    public static void Run(IReadOnlyList<SyntaxTree> trees, SymbolTable symbols, ReferenceIndex references, CompilationOptions options, List<Diagnostic> diagnostics)
    {
        var checks = new StyleChecks(symbols, references, options, diagnostics);
        foreach (SyntaxTree tree in trees)
        {
            checks.Tree = tree;
            checks.CheckFile();
        }

        checks.CheckRepeats(trees);
        checks.CheckUnusedVariables();
    }

    private FileSettings SettingsOf(SyntaxTree tree) => options.Settings.GetValueOrDefault(tree.Source.Path, FileSettings.None);

    private void CheckFile()
    {
        CheckNames();
        CheckIndentation();
        foreach (NodeSyntax node in Tree.Root.Nodes)
        {
            CheckNesting(node.Body, depth: 0, Settings.Threshold("pibbles_max_nesting"));
            CheckPoses(node.Body);
            CheckReturn(node);
            foreach (StatementSyntax statement in SyntaxWalk.Statements(node.Body))
                CheckStatement(statement);
        }
    }

    private void CheckStatement(StatementSyntax statement)
    {
        switch (statement)
        {
            case TextLineSyntax line:
                CheckPauses(line.Content);
                CheckLength(line.Content, option: false);
                CheckSpeakerSpacing(line);
                break;

            case ChoiceSyntax choice:
                foreach (OptionSyntax option in choice.Options)
                {
                    CheckOptionBody(option);
                    CheckLength(option.Text, option: true);
                }
                break;

            case IfStatementSyntax @if:
                CheckBranches(@if);
                break;

            case SetStatementSyntax set:
                CheckCompoundAssignment(set);
                break;
        }

        foreach (IReadOnlyList<InlineSyntax> content in SyntaxWalk.TextOf(statement))
        {
            CheckMarkup(content, new HashSet<string>());
            foreach (ConditionalTextSyntax conditional in SyntaxWalk.Runs(content).SelectMany(run => run.OfType<ConditionalTextSyntax>()))
                CheckBranches(conditional);
        }

        foreach (ExpressionSyntax expression in SyntaxWalk.ExpressionsOf(statement))
            CheckComparisonWithBool(expression);
    }

    private int LineOf(int position) => Tree.Source.GetLinePosition(position).Line;

    private string LineText(int line)
    {
        TextSpan span = Tree.Source.GetLineSpan(line);
        return Tree.Source.Text.Substring(span.Start, span.Length);
    }

    /// <summary>Whether a line holds something, rather than being blank or a comment.</summary>
    private bool HoldsContent(int line) => LineClassifier.KindOf(LineText(line).AsSpan().TrimStart(" \t")) is not (LineKind.Blank or LineKind.Comment or LineKind.Note);

    /// <summary>The text from the start of a run's first item to the end of its last.</summary>
    private static TextSpan SpanOf(IReadOnlyList<InlineSyntax> content) => new(content[0].Span.Start, content[^1].Span.End - content[0].Span.Start);
}
