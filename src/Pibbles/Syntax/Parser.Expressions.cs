using System.Globalization;
using System.Text;
using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

internal sealed partial class Parser
{
    /// <summary>
    /// Parses an expression, one method per precedence level, from lowest to highest: <c>or</c>; <c>and</c>; <c>not</c>;
    /// <c>== !=</c>; <c>&lt; &lt;= &gt; &gt;=</c>; <c>+ -</c>; <c>* / %</c>; unary <c>-</c>. Comparisons don't chain.
    /// </summary>
    private ExpressionSyntax ParseExpression() => ParseOr();

    private ExpressionSyntax ParseOr() => ParseOperators(ParseAnd, () => IsWord("or") ? BinaryOperator.Or : null);

    private ExpressionSyntax ParseAnd() => ParseOperators(ParseNot, () => IsWord("and") ? BinaryOperator.And : null);

    private ExpressionSyntax ParseNot()
    {
        if (!IsWord("not"))
            return ParseEquality();

        TextSpan operatorSpan = token.Span;
        Advance();
        ExpressionSyntax operand = ParseNot();
        return new UnaryExpressionSyntax(UnaryOperator.Not, operatorSpan, operand) { Span = new(operatorSpan.Start, operand.Span.End - operatorSpan.Start) };
    }

    private ExpressionSyntax ParseEquality() => ParseOperators(ParseRelational, () => token.Kind switch
    {
        TokenKind.EqualsEquals => BinaryOperator.Equals,
        TokenKind.BangEquals => BinaryOperator.NotEquals,
        _ => null,
    }, chains: false);

    private ExpressionSyntax ParseRelational() => ParseOperators(ParseAdditive, () => token.Kind switch
    {
        TokenKind.Less => BinaryOperator.Less,
        TokenKind.LessEquals => BinaryOperator.LessOrEqual,
        TokenKind.Greater => BinaryOperator.Greater,
        TokenKind.GreaterEquals => BinaryOperator.GreaterOrEqual,
        _ => null,
    }, chains: false);

    private ExpressionSyntax ParseAdditive() => ParseOperators(ParseMultiplicative, () => token.Kind switch
    {
        TokenKind.Plus => BinaryOperator.Add,
        TokenKind.Minus => BinaryOperator.Subtract,
        _ => null,
    });

    private ExpressionSyntax ParseMultiplicative() => ParseOperators(ParseUnary, () => token.Kind switch
    {
        TokenKind.Star => BinaryOperator.Multiply,
        TokenKind.Slash => BinaryOperator.Divide,
        TokenKind.Percent => BinaryOperator.Remainder,
        _ => null,
    });

    /// <summary>
    /// Parses operands joined by the operators of one level, left-associatively. A level that doesn't chain reports a
    /// second operator, then keeps going, so the tree is still complete.
    /// </summary>
    private ExpressionSyntax ParseOperators(Func<ExpressionSyntax> parseOperand, Func<BinaryOperator?> matchOperator, bool chains = true)
    {
        ExpressionSyntax left = parseOperand();
        int count = 0;
        while (matchOperator() is { } @operator)
        {
            TextSpan operatorSpan = token.Span;
            Advance();
            ExpressionSyntax right = parseOperand();
            left = new BinaryExpressionSyntax(left, @operator, operatorSpan, right) { Span = new(left.Span.Start, right.Span.End - left.Span.Start) };
            count++;
        }

        if (!chains && count > 1)
            Fail(DiagnosticCatalog.ChainedComparison, left.Span, TextOf(left.Span));

        return left;
    }

    private ExpressionSyntax ParseUnary()
    {
        if (token.Kind is not TokenKind.Minus)
            return ParsePrimary(argument: false);

        TextSpan operatorSpan = token.Span;
        Advance();
        ExpressionSyntax operand = ParseUnary();
        return new UnaryExpressionSyntax(UnaryOperator.Negate, operatorSpan, operand) { Span = new(operatorSpan.Start, operand.Span.End - operatorSpan.Start) };
    }

    /// <summary>
    /// Parses a literal, variable, name, call or parenthesized expression. In a command argument, a name followed by a
    /// <c>(</c> with a space between is two arguments; anywhere else, the space is reported.
    /// </summary>
    private ExpressionSyntax ParsePrimary(bool argument)
    {
        Token current = token;
        switch (current.Kind)
        {
            case TokenKind.Number:
                Advance();
                return ParseNumber(TextOf(current.Span), current.Span) is { } number
                    ? new NumberLiteralSyntax(number) { Span = current.Span }
                    : new ErrorExpressionSyntax { Span = current.Span };

            case TokenKind.Duration:
                Advance();
                string duration = TextOf(current.Span);
                decimal? seconds = duration.EndsWith("ms", StringComparison.Ordinal) ? ParseNumber(duration[..^2], current.Span) / 1000m : ParseNumber(duration[..^1], current.Span);
                return seconds is { } value
                    ? new DurationLiteralSyntax(value) { Span = current.Span }
                    : new ErrorExpressionSyntax { Span = current.Span };

            case TokenKind.String:
                Advance();
                return new StringLiteralSyntax(Unquote(TextOf(current.Span))) { Span = current.Span };

            case TokenKind.Variable:
                Advance();
                return new VariableExpressionSyntax(TextOf(current.Span)[1..]) { Span = current.Span };

            case TokenKind.Name:
                Advance();
                return ParseNameValue(current, argument);

            case TokenKind.OpenParen:
                Advance();
                ExpressionSyntax inner = ParseExpression();
                ExpectCloseParen(current.Span);
                return new ParenthesizedExpressionSyntax(inner) { Span = SpanFrom(current.Span.Start) };

            default:
                return MissingValue();
        }
    }

    /// <summary>Finishes a value that starts with an already-read name: a boolean, a call or a bare name.</summary>
    private ExpressionSyntax ParseNameValue(Token name, bool argument)
    {
        string text = TextOf(name.Span);
        if (text is "true" or "false")
            return new BooleanLiteralSyntax(text is "true") { Span = name.Span };

        if (token.Kind is TokenKind.CallOpen)
            return ParseCall(name);

        if (token.Kind is TokenKind.OpenParen && !argument)
        {
            Fail(DiagnosticCatalog.SpaceBeforeCall, new(name.Span.End, token.Span.Start - name.Span.End), text);
            return ParseCall(name);
        }

        return new NameExpressionSyntax(text) { Span = name.Span };
    }

    private CallExpressionSyntax ParseCall(Token name)
    {
        TextSpan open = token.Span;
        Advance();

        List<ExpressionSyntax> arguments = [];
        if (token.Kind is not TokenKind.CloseParen)
        {
            arguments.Add(ParseExpression());
            while (token.Kind is TokenKind.Comma)
            {
                Advance();
                arguments.Add(ParseExpression());
            }
        }

        ExpectCloseParen(open);
        var function = new NameSyntax(TextOf(name.Span)) { Span = name.Span };
        return new(function, arguments) { Span = SpanFrom(name.Span.Start) };
    }

    private void ExpectCloseParen(TextSpan open)
    {
        if (token.Kind is TokenKind.CloseParen)
            Advance();
        else
            Fail(DiagnosticCatalog.UnclosedParenthesis, open);
    }

    /// <summary>Reports a value missing after the previous token, and stands in for it with a zero-length error node.</summary>
    private ErrorExpressionSyntax MissingValue()
    {
        Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "a value", TextOf(previousSpan));
        return new() { Span = new(previousEnd, 0) };
    }

    private bool IsWord(string word) => token.Kind is TokenKind.Name && TextOf(token.Span) == word;

    /// <summary>
    /// Reads a number the lexer accepted. Returns <see langword="null"/> for one it reported as malformed, and for one too
    /// large for a <see cref="decimal"/>, which is reported here. Fraction digits past what a <see cref="decimal"/> holds round.
    /// </summary>
    private decimal? ParseNumber(string text, TextSpan literal)
    {
        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
            return value;

        if (text.All(character => char.IsAsciiDigit(character) || character is '.') && text.Count(character => character is '.') <= 1)
            Fail(DiagnosticCatalog.NumberTooLarge, literal, TextOf(literal));

        return null;
    }

    /// <summary>The text between a string's quotes, with each backslash removed and the character after it kept.</summary>
    private static string Unquote(string text)
    {
        var value = new StringBuilder();
        for (int i = 1; i < text.Length && text[i] is not '"'; i++)
        {
            if (text[i] is '\\')
                i++;

            if (i < text.Length)
                value.Append(text[i]);
        }

        return value.ToString();
    }
}
