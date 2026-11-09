using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Expressions: binding an expression to its type, with names read against the type expected, operators, calls and node references.</summary>
internal sealed partial class Binder
{
    /// <summary>Binds an expression and returns its type.</summary>
    /// <param name="expression">The expression to bind.</param>
    /// <param name="expected">The type expected where the expression appears, which a bare name is read against.</param>
    private TypeSymbol Bind(ExpressionSyntax expression, TypeSymbol? expected = null)
    {
        TypeSymbol type = BindCore(expression, expected);
        bindings.BindType(expression, type);
        return type;
    }

    private TypeSymbol BindCore(ExpressionSyntax expression, TypeSymbol? expected) => expression switch
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
        if (FullName(variable.Name, variable.Span, sigil: "$") is not { } name)
            return TypeSymbol.Error;

        if (symbols.Variables.TryGetValue(name, out VariableSymbol? symbol))
        {
            Refers(variable, symbol);
            return symbol.Type;
        }

        string? closest = Suggestions.Closest(name, symbols.Variables.Keys);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownVariable, variable.Span, $"${variable.Name}", closest is null ? null : $"${AsWritten(closest, variable.Name)}");
        return TypeSymbol.Error;
    }

    /// <summary>Reads a bare name against the expected type: a member of an enum, an actor or a node. No other type has names.</summary>
    private TypeSymbol BindName(NameExpressionSyntax name, TypeSymbol? expected)
    {
        if (expected == TypeSymbol.Error || expected is EnumSymbol { Members: [] })
            return TypeSymbol.Error;

        if (expected == TypeSymbol.Node)
            return BindNode(name.Name, name.Span, name);

        if (expected is EnumSymbol @enum)
        {
            if (@enum.Members.FirstOrDefault(member => member.Name == name.Name) is not { } member)
                return NotValueOf(name, @enum, @enum.Members.Select(member => member.Name), $"Use one of {Phrase.Or(@enum.Members.Select(member => member.Name))}.");

            Refers(name, member);
            return @enum;
        }

        if (expected == TypeSymbol.Actor)
        {
            if (!symbols.Actors.TryGetValue(name.Name, out ActorSymbol? actor))
                return NotValueOf(name, TypeSymbol.Actor, symbols.Actors.Keys, null);

            Refers(name, actor);
            return TypeSymbol.Actor;
        }

        string? meant = symbols.Variables.ContainsKey(name.Name) ? DiagnosticCatalog.DidYouMean($"${name.Name}").TrimEnd()
            : expected == TypeSymbol.String ? $"If it's text, put it in quotes: `\"{name.Name}\"`."
            : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NameNotAllowed, name.Span, name.Name, meant);
        return TypeSymbol.Error;
    }

    private TypeSymbol NotValueOf(NameExpressionSyntax name, TypeSymbol type, IEnumerable<string> values, string? otherwise)
    {
        string? help = Suggestions.Closest(name.Name, values) is { } closest ? DiagnosticCatalog.DidYouMean(closest).TrimEnd() : otherwise;
        ReportWithOptionalHelp(DiagnosticCatalog.NotValueOfType, name.Span, name.Name, type.Describe(), help);
        return TypeSymbol.Error;
    }

    private TypeSymbol BindNode(string written, TextSpan span, SyntaxNode key)
    {
        if (FullName(written, span) is not { } name)
            return TypeSymbol.Error;

        if (symbols.Nodes.TryGetValue(name, out NodeSymbol? node))
        {
            Refers(key, span, node);
            return TypeSymbol.Node;
        }

        if (symbols.Aliases.TryGetValue(name, out NodeSymbol? current))
        {
            Refers(key, span, current);
            Report(DiagnosticCatalog.OldNodeName, span, written, AsWritten(current.Name, written));
            return TypeSymbol.Node;
        }

        string? closest = Suggestions.Closest(name, symbols.Nodes.Keys);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownNode, span, written, closest is null ? null : AsWritten(closest, written));
        return TypeSymbol.Error;
    }

    /// <summary>Writes a full node or variable name the way <paramref name="written"/> is: relative to the file's prefix if it is, and the name is under it.</summary>
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

        Refers(call.Function, function);
        ArgumentSyntax[] arguments = [.. call.Arguments.Select(argument => new ArgumentSyntax(null, argument) { Span = argument.Span })];
        BindArguments(function, function.Parameters, arguments, call.Span, named: false);
        return function.ReturnType;
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
        if (IsChainedComparison(binary))
        {
            Bind(binary.Left, TypeSymbol.Error);
            Bind(binary.Right, TypeSymbol.Error);
            return TypeSymbol.Bool;
        }

        (TypeSymbol left, TypeSymbol right) = binary.Operator is BinaryOperator.Equals or BinaryOperator.NotEquals or BinaryOperator.Add
            ? BindNamedOperands(binary)
            : (Bind(binary.Left), Bind(binary.Right));

        TypeSymbol fallback = binary.Operator is BinaryOperator.Or or BinaryOperator.And or BinaryOperator.Equals or BinaryOperator.NotEquals
            or BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual
            ? TypeSymbol.Bool
            : TypeSymbol.Error;
        if (left == TypeSymbol.Error || right == TypeSymbol.Error)
            return fallback;

        if (OperatorType(binary.Operator, left, right) is { } result)
        {
            if (binary.Operator is BinaryOperator.Divide or BinaryOperator.Remainder && ConstantValue.Fold(binary.Right) == 0m)
                Report(DiagnosticCatalog.DivisionByZero, binary.Span, TextOf(binary.Span));

            if (ConstantValue.Overflows(binary))
                Report(DiagnosticCatalog.ConstantOverflow, binary.Span, TextOf(binary.Span));

            if (binary.Operator is not (BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Remainder))
            {
                bindings.ConvertTo(binary.Left, right);
                bindings.ConvertTo(binary.Right, left);
            }

            return result;
        }

        Report(DiagnosticCatalog.OperatorTypes, binary.Span, TextOf(binary.OperatorSpan), $"{left.Describe()} and {right.Describe()}", OperatorHelp(binary.Operator));
        return fallback;
    }

    /// <summary>
    /// Binds the operands of <c>==</c>, <c>!=</c> or <c>+</c>. A bare name on one side is read against the other side's
    /// type, so that side is bound first. Two bare names have no type to be read against: <c>==</c> and <c>!=</c> report
    /// them together, and <c>+</c> reports each.
    /// </summary>
    private (TypeSymbol Left, TypeSymbol Right) BindNamedOperands(BinaryExpressionSyntax binary)
    {
        bool leftIsName = Unwrap(binary.Left) is NameExpressionSyntax;
        bool rightIsName = Unwrap(binary.Right) is NameExpressionSyntax;
        if (leftIsName && rightIsName)
        {
            if (binary.Operator is BinaryOperator.Add)
                return (Bind(binary.Left), Bind(binary.Right));

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

    /// <summary>
    /// Whether a comparison has another comparison of the same level as its left operand, with no brackets, as in
    /// <c>$a &lt; $b &lt; $c</c>. Comparisons don't chain, and the parser has already reported it (PIB3002).
    /// </summary>
    private static bool IsChainedComparison(BinaryExpressionSyntax binary) =>
        binary.Left is BinaryExpressionSyntax inner && ComparisonLevel(inner.Operator) is { } level && level == ComparisonLevel(binary.Operator);

    private static int? ComparisonLevel(BinaryOperator @operator) => @operator switch
    {
        BinaryOperator.Equals or BinaryOperator.NotEquals => 1,
        BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual => 2,
        _ => null,
    };

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

    /// <summary>
    /// Whether a value of type <paramref name="from"/> can go where <paramref name="to"/> is expected: the same type, a
    /// number where a duration is, or a type that can't be known, which includes an enum with no members, since it's
    /// already reported.
    /// </summary>
    private static bool Converts(TypeSymbol from, TypeSymbol to) =>
        from == to || from == TypeSymbol.Error || to == TypeSymbol.Error || to is EnumSymbol { Members: [] } || (from == TypeSymbol.Number && to == TypeSymbol.Duration);

    /// <summary>The type a declaration names. A type that doesn't exist has already been reported by the declaration pass.</summary>
    private TypeSymbol TypeNamed(NameSyntax name) => symbols.FindType(name.Text) ?? TypeSymbol.Error;
}
