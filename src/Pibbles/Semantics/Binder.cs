using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Resolves the names in a story against its <see cref="SymbolTable"/>: speakers and their values, commands, markup, icons,
/// tags, functions, variables and nodes. It checks every argument against its parameter, and the type of every
/// expression: in variables' values, parameters' defaults, conditions, arguments, <c>@set</c>, <c>@pause</c> and text.
/// </summary>
/// <remarks>
/// An expression is bound with the type expected where it appears, which is how a bare name is read: as a member of the
/// expected enum, an actor or a node. Anything whose type can't be known gets <see cref="TypeSymbol.Error"/>, which
/// converts to and from every type, so each problem is reported once.
/// </remarks>
internal sealed partial class Binder(SymbolTable symbols, List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings) : AnalysisPass(diagnostics, references)
{
    /// <summary>Binds every file's declarations and node bodies against the symbol table.</summary>
    /// <param name="trees">The story's files.</param>
    /// <param name="symbols">The symbol table the declaration pass made.</param>
    /// <param name="diagnostics">Where problems with names, types and arguments are reported.</param>
    /// <param name="references">Where each name that refers to a symbol is recorded.</param>
    /// <param name="bindings">Where each name, expression and argument is bound.</param>
    public static void Run(IReadOnlyList<SyntaxTree> trees, SymbolTable symbols, List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings)
    {
        var binder = new Binder(symbols, diagnostics, references, bindings);
        foreach (SyntaxTree tree in trees)
        {
            binder.Tree = tree;
            binder.BindDeclarations(tree.Root.Declarations);
            foreach (NodeSyntax node in tree.Root.Nodes)
                binder.BindStatements(node.Body);
        }
    }

    /// <summary>Records that <paramref name="name"/> refers to <paramref name="symbol"/>, in the reference index and in the bindings.</summary>
    private void Refers(SyntaxNode name, Symbol symbol) => Refers(name, name.Span, symbol);

    /// <summary>Records that <paramref name="name"/>, whose reference is the text at <paramref name="span"/>, refers to <paramref name="symbol"/>.</summary>
    private void Refers(SyntaxNode name, TextSpan span, Symbol symbol)
    {
        Refers(span, symbol);
        bindings.BindName(name, symbol);
    }

    private void BindDeclarations(IEnumerable<DeclarationSyntax> declarations)
    {
        foreach (DeclarationSyntax declaration in declarations)
        {
            if (declaration is VariableDeclarationSyntax variable)
            {
                TypeSymbol type = variable.Type is { } written ? TypeNamed(written) : DeclarationPass.TypeOfStartingValue(variable.Value);
                BindValue(variable.Value, type, $"`${variable.Variable.Name}` holds {type.Describe()}");
            }

            IReadOnlyList<ParameterSyntax> parameters = declaration switch
            {
                CommandDeclarationSyntax command => command.Parameters,
                MarkupDeclarationSyntax markup => markup.Parameters,
                FunctionDeclarationSyntax function => function.Parameters,
                _ => [],
            };

            foreach (ParameterSyntax parameter in parameters.Where(parameter => parameter.Default is not null))
            {
                TypeSymbol type = TypeNamed(parameter.Type);
                BindValue(parameter.Default!, type, $"`{parameter.Name.Text}` takes {type.Describe()}");
            }
        }
    }

    private void BindStatements(IEnumerable<StatementSyntax> statements)
    {
        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case IfStatementSyntax @if:
                    BindCondition(@if.Condition);
                    BindStatements(@if.Body);
                    foreach (ElseIfClauseSyntax elseIf in @if.ElseIfs)
                    {
                        BindCondition(elseIf.Condition);
                        BindStatements(elseIf.Body);
                    }

                    BindStatements(@if.Else?.Body ?? []);
                    break;

                case TextLineSyntax line:
                    BindTextLine(line);
                    break;

                case ChoiceSyntax choice:
                    foreach (OptionSyntax option in choice.Options)
                    {
                        BindInline(option.Text);
                        if (option.Condition is { } condition)
                            BindCondition(condition);

                        BindTags(option.Tags);
                        BindStatements(option.Body);
                    }

                    break;

                case CommandStatementSyntax command:
                    BindCommand(command.Command, command.Arguments, command.Span, inline: false);
                    break;

                case JumpStatementSyntax jump:
                    BindNode(jump.Target.Text, jump.Target.Span, jump.Target);
                    break;

                case CallStatementSyntax call:
                    BindNode(call.Target.Text, call.Target.Span, call.Target);
                    BindTags(call.Tags);
                    break;

                case VariationStatementSyntax variation:
                    foreach (AlternativeSyntax alternative in variation.Alternatives)
                        BindStatements(alternative.Body);
                    break;

                case OnceStatementSyntax once:
                    BindStatements(once.Body);
                    break;

                case SetStatementSyntax set:
                    BindSet(set);
                    break;

                case PauseStatementSyntax pause:
                    BindPacing(pause.Duration, TypeSymbol.Duration, "`@pause` takes a `duration`", "The time to `@pause`", "Write a time longer than zero, like `@pause 0.5s`.");
                    break;
            }
        }
    }

    private void BindSet(SetStatementSyntax set)
    {
        TypeSymbol target = Bind(set.Variable);
        string holds = $"`${set.Variable.Name}` holds {target.Describe()}";
        if (set.Operator is AssignmentOperator.Assign)
        {
            BindValue(set.Value, target, holds);
            return;
        }

        TypeSymbol value = Bind(set.Value, target);
        if (target == TypeSymbol.Error || value == TypeSymbol.Error)
            return;

        BinaryOperator @operator = set.Operator is AssignmentOperator.Add ? BinaryOperator.Add : BinaryOperator.Subtract;
        TypeSymbol? result = OperatorType(@operator, target, value);
        if (result is null)
            Report(DiagnosticCatalog.OperatorTypes, new(set.Variable.Span.Start, set.Value.Span.End - set.Variable.Span.Start), TextOf(set.OperatorSpan), $"{target.Describe()} and {value.Describe()}", OperatorHelp(@operator));
        else if (result != target)
            ReportWithOptionalHelp(DiagnosticCatalog.ValueType, set.Value.Span, holds, value.Describe(), ValueHelp(target));
        else
            bindings.ConvertTo(set.Value, target);
    }

    /// <summary>Binds a value that has to have <paramref name="target"/>'s type, reporting it with what it's for if it doesn't.</summary>
    /// <returns>Whether the value's type converts to <paramref name="target"/>.</returns>
    private bool BindValue(ExpressionSyntax value, TypeSymbol target, string purpose)
    {
        TypeSymbol type = Bind(value, target);
        if (Converts(type, target))
        {
            bindings.ConvertTo(value, target);
            return true;
        }

        ReportWithOptionalHelp(DiagnosticCatalog.ValueType, value.Span, purpose, type.Describe(), ValueHelp(target));
        return false;
    }

    /// <summary>
    /// Binds a pacing value, and reports it if it has the right type but is a constant that isn't more than zero. A value
    /// computed at run time is checked by the runtime.
    /// </summary>
    private void BindPacing(ExpressionSyntax value, TypeSymbol target, string purpose, string what, string help)
    {
        if (BindValue(value, target, purpose) && ConstantValue.Fold(value) is <= 0)
            Report(DiagnosticCatalog.NotPositive, value.Span, what, TextOf(value.Span), help);
    }

    private void BindCondition(ExpressionSyntax condition)
    {
        TypeSymbol type = Bind(condition, TypeSymbol.Bool);
        if (Converts(type, TypeSymbol.Bool))
            return;

        string text = TextOf(condition.Span);
        string? meant = type == TypeSymbol.Number ? $"{text} > 0" : type == TypeSymbol.String ? $"{text} != \"\"" : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NotCondition, condition.Span, text, type.Describe(), meant);
    }
}
