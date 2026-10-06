using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Resolves the names in a story against its <see cref="SymbolTable"/>, and checks the type of every expression: in
/// variables' values, parameters' defaults, conditions, <c>@set</c> and <c>@wait</c>.
/// </summary>
/// <remarks>
/// An expression is bound with the type expected where it appears, which is how a bare name is read: as a member of the
/// expected enum, an actor or a node. Anything whose type can't be known gets <see cref="TypeSymbol.Error"/>, which
/// converts to and from every type, so each problem is reported once.
/// </remarks>
internal sealed class Binder(SymbolTable symbols, List<Diagnostic> diagnostics) : AnalysisPass(diagnostics)
{
    public static void Run(IReadOnlyList<SyntaxTree> trees, SymbolTable symbols, List<Diagnostic> diagnostics)
    {
        var binder = new Binder(symbols, diagnostics);
        foreach (SyntaxTree tree in trees)
        {
            binder.Tree = tree;
            binder.BindDeclarations(tree.Root.Declarations);
            foreach (NodeSyntax node in tree.Root.Nodes)
                binder.BindStatements(node.Body);
        }
    }

    private void BindDeclarations(IEnumerable<DeclarationSyntax> declarations)
    {
        foreach (DeclarationSyntax declaration in declarations)
        {
            if (declaration is VariableDeclarationSyntax { Type: { } written } variable)
            {
                TypeSymbol type = TypeNamed(written);
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

                case ChoiceSyntax choice:
                    foreach (OptionSyntax option in choice.Options)
                    {
                        if (option.Condition is { } condition)
                            BindCondition(condition);

                        BindStatements(option.Body);
                    }

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

                case WaitStatementSyntax wait:
                    BindValue(wait.Duration, TypeSymbol.Duration, "`@wait` takes a `duration`");
                    break;
            }
        }
    }

    private void BindSet(SetStatementSyntax set)
    {
        TypeSymbol target = BindVariable(set.Variable);
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
    }

    /// <summary>Binds a value that has to have <paramref name="target"/>'s type, reporting it with what it's for if it doesn't.</summary>
    private void BindValue(ExpressionSyntax value, TypeSymbol target, string purpose)
    {
        TypeSymbol type = Bind(value, target);
        if (!Converts(type, target))
            ReportWithOptionalHelp(DiagnosticCatalog.ValueType, value.Span, purpose, type.Describe(), ValueHelp(target));
    }

    private void BindCondition(ExpressionSyntax condition)
    {
        TypeSymbol type = Bind(condition, TypeSymbol.Bool);
        if (Converts(type, TypeSymbol.Bool))
            return;

        string text = TextOf(condition.Span);
        string? meant = type == TypeSymbol.Number ? $"Did you mean `{text} > 0`?" : type == TypeSymbol.String ? $"Did you mean `{text} != \"\"`?" : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NotCondition, condition.Span, text, type.Describe(), meant);
    }

    /// <summary>Binds an expression and returns its type.</summary>
    /// <param name="expression">The expression to bind.</param>
    /// <param name="expected">The type expected where the expression appears, which a bare name is read against.</param>
    private TypeSymbol Bind(ExpressionSyntax expression, TypeSymbol? expected = null) => expression switch
    {
        NumberLiteralSyntax => TypeSymbol.Number,
        DurationLiteralSyntax => TypeSymbol.Duration,
        StringLiteralSyntax => TypeSymbol.String,
        BooleanLiteralSyntax => TypeSymbol.Bool,
        VariableExpressionSyntax variable => BindVariable(variable),
        NameExpressionSyntax name => BindName(name, expected),
        CallExpressionSyntax call => BindCall(call),
        ParenthesizedExpressionSyntax parenthesized => Bind(parenthesized.Expression, expected),
        UnaryExpressionSyntax unary => BindUnary(unary),
        BinaryExpressionSyntax binary => BindBinary(binary),
        _ => TypeSymbol.Error,
    };

    private TypeSymbol BindVariable(VariableExpressionSyntax variable)
    {
        if (variable.Name.Length == 0)
            return TypeSymbol.Error;

        if (symbols.Variables.TryGetValue(variable.Name, out VariableSymbol? symbol))
            return symbol.Type;

        string? closest = Suggestions.Closest(variable.Name, symbols.Variables.Keys);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownVariable, variable.Span, $"${variable.Name}", closest is null ? null : $"${closest}");
        return TypeSymbol.Error;
    }

    /// <summary>Reads a bare name against the expected type: a member of an enum, an actor or a node. No other type has names.</summary>
    private TypeSymbol BindName(NameExpressionSyntax name, TypeSymbol? expected)
    {
        if (expected == TypeSymbol.Error)
            return TypeSymbol.Error;

        if (expected == TypeSymbol.Node)
            return BindNode(name.Name, name.Span);

        if (expected is EnumSymbol @enum)
        {
            return @enum.Members.Any(member => member.Name == name.Name)
                ? @enum
                : NotValueOf(name, @enum, @enum.Members.Select(member => member.Name), $"Use one of {Phrase.Or(@enum.Members.Select(member => member.Name))}.");
        }

        if (expected == TypeSymbol.Actor)
            return symbols.Actors.ContainsKey(name.Name) ? TypeSymbol.Actor : NotValueOf(name, TypeSymbol.Actor, symbols.Actors.Keys, null);

        string? meant = symbols.Variables.ContainsKey(name.Name) ? $"Did you mean `${name.Name}`?"
            : expected == TypeSymbol.String ? $"If it's text, put it in quotes: `\"{name.Name}\"`."
            : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NameNotAllowed, name.Span, name.Name, meant);
        return TypeSymbol.Error;
    }

    private TypeSymbol NotValueOf(NameExpressionSyntax name, TypeSymbol type, IEnumerable<string> values, string? otherwise)
    {
        string? help = Suggestions.Closest(name.Name, values) is { } closest ? $"Did you mean `{closest}`?" : otherwise;
        ReportWithOptionalHelp(DiagnosticCatalog.NotValueOfType, name.Span, name.Name, type.Describe(), help);
        return TypeSymbol.Error;
    }

    private TypeSymbol BindNode(string written, TextSpan span)
    {
        if (FullName(written, span) is not { } name)
            return TypeSymbol.Error;

        if (symbols.Nodes.ContainsKey(name))
            return TypeSymbol.Node;

        if (symbols.Aliases.TryGetValue(name, out NodeSymbol? current))
        {
            Report(DiagnosticCatalog.OldNodeName, span, written, AsWritten(current.Name, written));
            return TypeSymbol.Node;
        }

        string? closest = Suggestions.Closest(name, symbols.Nodes.Keys);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownNode, span, written, closest is null ? null : AsWritten(closest, written));
        return TypeSymbol.Error;
    }

    /// <summary>Writes a full node name the way <paramref name="written"/> is: relative to the file's prefix if it is, and the node is under it.</summary>
    private string AsWritten(string fullName, string written) =>
        written.StartsWith('.') && Tree.Root.Prefix?.Name.Text.TrimStart('.') is { } prefix && fullName.StartsWith(prefix + ".", StringComparison.Ordinal)
            ? fullName[prefix.Length..]
            : fullName;

    private TypeSymbol BindCall(CallExpressionSyntax call)
    {
        if (!symbols.Functions.TryGetValue(call.Function.Text, out FunctionSymbol? function))
        {
            if (!call.Function.IsMissing)
                ReportWithOptionalHelp(DiagnosticCatalog.UnknownFunction, call.Function.Span, call.Function.Text, Suggestions.Closest(call.Function.Text, symbols.Functions.Keys));

            foreach (ExpressionSyntax argument in call.Arguments)
                Bind(argument, TypeSymbol.Error);

            return TypeSymbol.Error;
        }

        BindArguments($"{function.Name}()", function.Parameters, call.Arguments, call.Span);
        return function.ReturnType;
    }

    /// <summary>Matches arguments to parameters in order, binding each against its parameter's type.</summary>
    private void BindArguments(string owner, IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<ExpressionSyntax> arguments, TextSpan span)
    {
        foreach ((ExpressionSyntax argument, ParameterSymbol? parameter) in arguments.Select((argument, index) => (argument, parameters.ElementAtOrDefault(index))))
        {
            TypeSymbol type = Bind(argument, parameter?.Type ?? TypeSymbol.Error);
            if (parameter is not null && !Converts(type, parameter.Type))
                ReportWithOptionalHelp(DiagnosticCatalog.ArgumentType, argument.Span, owner, parameter.Type.Describe(), parameter.Name, type.Describe(), ValueHelp(parameter.Type));
        }

        if (arguments.Count > parameters.Count)
        {
            string takes = parameters.Count switch { 0 => "no arguments", 1 => "only 1 argument", var count => $"only {count} arguments" };
            Report(DiagnosticCatalog.TooManyArguments, new(arguments[parameters.Count].Span.Start, arguments[^1].Span.End - arguments[parameters.Count].Span.Start), owner, takes);
        }

        string[] missing = [.. parameters.Skip(arguments.Count).Where(parameter => !parameter.IsOptional).Select(parameter => parameter.Name)];
        if (missing.Length > 0)
        {
            (string needs, string help) = missing.Length == 1
                ? ($"a value for {Phrase.And(missing)}", "Add it in the brackets, in order.")
                : ($"values for {Phrase.And(missing)}", "Add them in the brackets, in order.");
            Report(DiagnosticCatalog.MissingArgument, span, owner, needs, help);
        }
    }

    private TypeSymbol BindUnary(UnaryExpressionSyntax unary)
    {
        TypeSymbol operand = Bind(unary.Operand);
        TypeSymbol fallback = unary.Operator is UnaryOperator.Not ? TypeSymbol.Bool : TypeSymbol.Error;
        if (operand == TypeSymbol.Error)
            return fallback;

        if (unary.Operator is UnaryOperator.Not ? operand == TypeSymbol.Bool : operand == TypeSymbol.Number || operand == TypeSymbol.Duration)
            return operand;

        string help = unary.Operator is UnaryOperator.Not ? "`not` works on `true` or `false`." : "`-` makes a number or a duration negative.";
        Report(DiagnosticCatalog.OperatorTypes, unary.Span, TextOf(unary.OperatorSpan), operand.Describe(), help);
        return fallback;
    }

    private TypeSymbol BindBinary(BinaryExpressionSyntax binary)
    {
        (TypeSymbol left, TypeSymbol right) = binary.Operator is BinaryOperator.Equals or BinaryOperator.NotEquals
            ? BindEqualityOperands(binary)
            : (Bind(binary.Left), Bind(binary.Right));

        TypeSymbol fallback = binary.Operator is BinaryOperator.Or or BinaryOperator.And or BinaryOperator.Equals or BinaryOperator.NotEquals
            or BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual
            ? TypeSymbol.Bool
            : TypeSymbol.Error;
        if (left == TypeSymbol.Error || right == TypeSymbol.Error)
            return fallback;

        if (OperatorType(binary.Operator, left, right) is { } result)
            return result;

        Report(DiagnosticCatalog.OperatorTypes, binary.Span, TextOf(binary.OperatorSpan), $"{left.Describe()} and {right.Describe()}", OperatorHelp(binary.Operator));
        return fallback;
    }

    /// <summary>
    /// Binds the operands of <c>==</c> or <c>!=</c>. A bare name on one side is read against the other side's type, so
    /// that side is bound first. Two bare names have no type to be read against.
    /// </summary>
    private (TypeSymbol Left, TypeSymbol Right) BindEqualityOperands(BinaryExpressionSyntax binary)
    {
        bool leftIsName = Unwrap(binary.Left) is NameExpressionSyntax;
        bool rightIsName = Unwrap(binary.Right) is NameExpressionSyntax;
        if (leftIsName && rightIsName)
        {
            Report(DiagnosticCatalog.TwoNames, binary.Span, TextOf(binary.Span));
            return (TypeSymbol.Error, TypeSymbol.Error);
        }

        if (leftIsName)
        {
            TypeSymbol right = Bind(binary.Right);
            return (Bind(binary.Left, right), right);
        }

        TypeSymbol left = Bind(binary.Left);
        return (left, Bind(binary.Right, left));
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? Unwrap(parenthesized.Expression) : expression;

    /// <summary>
    /// The type an operator gives two operands, from the operator types in <c>docs/language/reference.md</c>, or
    /// <see langword="null"/> if it doesn't work on them. A number stands in for a duration where the other side is one,
    /// except in <c>*</c> and <c>/</c>, where a number is a factor.
    /// </summary>
    private static TypeSymbol? OperatorType(BinaryOperator @operator, TypeSymbol left, TypeSymbol right)
    {
        bool numbers = left == TypeSymbol.Number && right == TypeSymbol.Number;
        bool durations = !numbers && IsTime(left) && IsTime(right);

        return @operator switch
        {
            BinaryOperator.Or or BinaryOperator.And when left == TypeSymbol.Bool && right == TypeSymbol.Bool => TypeSymbol.Bool,
            BinaryOperator.Equals or BinaryOperator.NotEquals when left == right || durations => TypeSymbol.Bool,
            BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual when numbers || durations => TypeSymbol.Bool,
            BinaryOperator.Add when left == TypeSymbol.String && right == TypeSymbol.String => TypeSymbol.String,
            BinaryOperator.Add or BinaryOperator.Subtract when numbers => TypeSymbol.Number,
            BinaryOperator.Add or BinaryOperator.Subtract when durations => TypeSymbol.Duration,
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Remainder when numbers => TypeSymbol.Number,
            BinaryOperator.Multiply when (left == TypeSymbol.Duration && right == TypeSymbol.Number) || (left == TypeSymbol.Number && right == TypeSymbol.Duration) => TypeSymbol.Duration,
            BinaryOperator.Divide when left == TypeSymbol.Duration && right == TypeSymbol.Number => TypeSymbol.Duration,
            BinaryOperator.Divide when left == TypeSymbol.Duration && right == TypeSymbol.Duration => TypeSymbol.Number,
            _ => null,
        };

        static bool IsTime(TypeSymbol type) => type == TypeSymbol.Number || type == TypeSymbol.Duration;
    }

    private static string OperatorHelp(BinaryOperator @operator) => @operator switch
    {
        BinaryOperator.Or or BinaryOperator.And => "`and` and `or` join two conditions, each `true` or `false`.",
        BinaryOperator.Equals or BinaryOperator.NotEquals => "`==` and `!=` compare two values of the same type.",
        BinaryOperator.Add => "`+` adds two numbers or two durations, or joins two pieces of text.",
        BinaryOperator.Subtract => "`-` subtracts two numbers or two durations.",
        BinaryOperator.Multiply => "`*` multiplies two numbers, or a duration by a number.",
        BinaryOperator.Divide => "`/` divides a number by a number, or a duration by a number or a duration.",
        BinaryOperator.Remainder => "`%` gives the remainder of dividing two numbers.",
        _ => "`<`, `<=`, `>` and `>=` compare two numbers or two durations.",
    };

    /// <summary>Says what values a type takes, for the help of a value that has the wrong type.</summary>
    private string? ValueHelp(TypeSymbol type) => type switch
    {
        EnumSymbol @enum => $"Use one of {Phrase.Or(@enum.Members.Select(member => member.Name))}.",
        _ when type == TypeSymbol.Bool => "Use `true` or `false`.",
        _ when type == TypeSymbol.Number => "Write a number, like `1` or `0.5`.",
        _ when type == TypeSymbol.Duration => "Write a duration, like `0.5s` or `300ms`.",
        _ when type == TypeSymbol.String => "Put text in quotes, like `\"key\"`.",
        _ when type == TypeSymbol.Actor => symbols.Actors.Keys.FirstOrDefault() is { } actor ? $"Use an actor's ID, like `{actor}`." : null,
        _ when type == TypeSymbol.Node => symbols.Nodes.Keys.FirstOrDefault() is { } node ? $"Use a node's name, like `{node}`." : null,
        _ => null,
    };

    /// <summary>Whether a value of type <paramref name="from"/> can go where <paramref name="to"/> is expected: the same type, a number where a duration is, or a type that can't be known.</summary>
    private static bool Converts(TypeSymbol from, TypeSymbol to) =>
        from == to || from == TypeSymbol.Error || to == TypeSymbol.Error || (from == TypeSymbol.Number && to == TypeSymbol.Duration);

    /// <summary>The type a declaration names. A type that doesn't exist has already been reported by the declaration pass.</summary>
    private TypeSymbol TypeNamed(NameSyntax name) => symbols.FindType(name.Text) ?? TypeSymbol.Error;
}
