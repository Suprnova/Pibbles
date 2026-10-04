using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

internal sealed partial class Parser
{
    /// <summary>Keywords whose statements the parser doesn't read yet. Their lines become placeholders.</summary>
    private static readonly HashSet<string> UnparsedKeywords =
        ["@sequence", "@cycle", "@once", "@actor", "@enum", "@var", "@command", "@markup", "@icon", "@tag", "@function", "@term", "@resume", "@shuffle"];

    /// <summary>Parses the statement on the current line, and any block it owns, into <paramref name="statements"/>.</summary>
    private void ParseStatement(List<StatementSyntax> statements)
    {
        SourceLine line = Current.Line;
        if (line.Kind is not LineKind.At)
        {
            statements.Add(ParseUnparsedStatement(line));
            return;
        }

        StartLine(line.Content);
        string word = TextOf(token.Span);
        int start = line.Content.Start;
        switch (word)
        {
            case "@jump":
                Advance();
                NameSyntax destination = ExpectName("a node name", "@jump");
                statements.Add(FinishStatement(new JumpStatementSyntax(destination) { Span = SpanFrom(start) }));
                return;

            case "@call":
                Advance();
                NameSyntax target = ExpectName("a node name", "@call");
                List<TagSyntax> tags = [];
                while (token.Kind is TokenKind.Tag && !lineFailed)
                    tags.Add(ReadTag());

                statements.Add(FinishStatement(new CallStatementSyntax(target, tags) { Span = SpanFrom(start) }));
                return;

            case "@return":
                Advance();
                statements.Add(FinishStatement(new ReturnStatementSyntax { Span = SpanFrom(start) }));
                return;

            case "@end":
                Advance();
                statements.Add(FinishStatement(new EndStatementSyntax { Span = SpanFrom(start) }));
                return;

            case "@if":
                statements.Add(ParseIf(start));
                return;

            case "@elif" or "@else":
                ParseStrayClause(statements);
                return;

            case "@set":
                statements.Add(ParseSet(start));
                return;

            case "@wait":
                Advance();
                ExpressionSyntax duration = ParseExpression();
                statements.Add(FinishStatement(new WaitStatementSyntax(duration) { Span = SpanFrom(start) }));
                return;

            case "@prefix":
                ReportMisplacedPrefix();
                return;

            case var _ when UnparsedKeywords.Contains(word):
                SkipRestOfLine();
                statements.Add(ParseUnparsedStatement(line));
                return;

            case var _ when token.Kind is TokenKind.AtWord:
                statements.Add(ParseCommand(start));
                return;

            default:
                Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{word}`");
                index++;
                return;
        }
    }

    private StatementSyntax FinishStatement(StatementSyntax statement)
    {
        FinishLine();
        return statement;
    }

    private IfStatementSyntax ParseIf(int start)
    {
        TextSpan keyword = token.Span;
        Advance();
        ExpressionSyntax condition = ParseExpression();
        (List<StatementSyntax> body, int end) = ParseOpenerEnd("@if", keyword, start);

        List<ElseIfClauseSyntax> elseIfs = [];
        ElseClauseSyntax? @else = null;
        while (@else is null && Current.Kind is LineTokenKind.Line && Current.Line.Kind is LineKind.At && AtWordOf(Current.Line) is "@elif" or "@else")
        {
            int clauseStart = Current.Line.Content.Start;
            StartLine(Current.Line.Content);
            TextSpan clauseKeyword = token.Span;
            bool isElse = TextOf(clauseKeyword) is "@else";
            Advance();

            if (isElse)
            {
                (List<StatementSyntax> elseBody, end) = ParseOpenerEnd("@else", clauseKeyword, clauseStart);
                @else = new(elseBody) { Span = new(clauseStart, end - clauseStart) };
            }
            else
            {
                ExpressionSyntax elseIfCondition = ParseExpression();
                (List<StatementSyntax> elseIfBody, end) = ParseOpenerEnd("@elif", clauseKeyword, clauseStart);
                elseIfs.Add(new(elseIfCondition, elseIfBody) { Span = new(clauseStart, end - clauseStart) });
            }
        }

        return new(condition, body, elseIfs, @else) { Span = new(start, end - start) };
    }

    /// <summary>
    /// Reads the <c>:</c> that ends a block opener, finishes its line, and parses the block under it. A missing <c>:</c>
    /// is reported, but the line still owns its block, so the block isn't reported again as unexpected.
    /// Returns the block and where the statement ends: after its block, or after its line when it has none.
    /// </summary>
    private (List<StatementSyntax> Body, int End) ParseOpenerEnd(string keyword, TextSpan keywordSpan, int start)
    {
        if (token.Kind is TokenKind.Colon)
            Advance();
        else
            Fail(DiagnosticCatalog.MissingColon, new(previousEnd, 0), keyword, TextOf(new(start, previousEnd - start)));

        bool failed = lineFailed;
        int lineEnd = previousEnd;
        FinishLine();

        if (Current.Kind is LineTokenKind.Indent)
        {
            index++;
            List<StatementSyntax> body = ParseStatements(nodeLevel: false);
            SkipDedent();
            return (body, body.Count > 0 ? Math.Max(body[^1].Span.End, lineEnd) : lineEnd);
        }

        if (!failed)
            Report(DiagnosticCatalog.MissingBlock, keywordSpan, TextOf(new(start, lineEnd - start)));

        return ([], lineEnd);
    }

    /// <summary>Reports an <c>@elif</c> or <c>@else</c> with no <c>@if</c> above it. The statements in its block join the block around it.</summary>
    private void ParseStrayClause(List<StatementSyntax> statements)
    {
        Fail(DiagnosticCatalog.StrayClause, token.Span, TextOf(token.Span));
        index++;
        if (Current.Kind is not LineTokenKind.Indent)
            return;

        index++;
        statements.AddRange(ParseStatements(nodeLevel: false));
        SkipDedent();
    }

    private SetStatementSyntax ParseSet(int start)
    {
        Advance();
        VariableExpressionSyntax variable;
        if (token.Kind is TokenKind.Variable)
        {
            variable = new(TextOf(token.Span)[1..]) { Span = token.Span };
            Advance();
        }
        else
        {
            Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "a variable", "@set");
            variable = new("") { Span = new(previousEnd, 0) };
        }

        AssignmentOperator? assignment = token.Kind switch
        {
            TokenKind.Equals => AssignmentOperator.Assign,
            TokenKind.PlusEquals => AssignmentOperator.Add,
            TokenKind.MinusEquals => AssignmentOperator.Subtract,
            _ => null,
        };

        TextSpan operatorSpan = token.Span;
        if (assignment is null)
        {
            Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "`=`, `+=` or `-=`", TextOf(previousSpan));
            operatorSpan = new(previousEnd, 0);
        }
        else
        {
            Advance();
        }

        ExpressionSyntax value = ParseExpression();
        FinishLine();
        return new(variable, assignment ?? AssignmentOperator.Assign, operatorSpan, value) { Span = new(start, value.Span.End - start) };
    }

    private CommandStatementSyntax ParseCommand(int start)
    {
        var command = new NameSyntax(TextOf(token.Span)[1..]) { Span = new(token.Span.Start + 1, token.Span.Length - 1) };
        Advance();

        List<ArgumentSyntax> arguments = [];
        CommandWait wait = CommandWait.Default;
        while (!AtLineEnd && !lineFailed)
        {
            if (token.Kind is TokenKind.Name && TextOf(token.Span) is "wait" or "nowait")
            {
                wait = TextOf(token.Span) is "wait" ? CommandWait.Wait : CommandWait.NoWait;
                Advance();
                break;
            }

            if (ParseArgument() is not { } argument)
                break;

            if (argument.Name is null && arguments.Any(previous => previous.Name is not null))
                Fail(DiagnosticCatalog.ArgumentOrder, argument.Span);

            arguments.Add(argument);
        }

        int end = previousEnd;
        FinishLine();
        return new(command, arguments, wait) { Span = new(start, end - start) };
    }

    /// <summary>Reads one argument: <c>name=value</c>, or a value on its own. Returns <see langword="null"/> after reporting a token that starts neither.</summary>
    private ArgumentSyntax? ParseArgument()
    {
        if (token.Kind is not TokenKind.Name)
            return ParseArgumentValue() is { } value ? new(null, value) { Span = value.Span } : null;

        Token name = token;
        Advance();
        if (token.Kind is not TokenKind.Equals)
        {
            ExpressionSyntax positional = ParseNameValue(name, argument: true);
            return new(null, positional) { Span = positional.Span };
        }

        Advance();
        if ((AtLineEnd ? MissingValue() : ParseArgumentValue()) is not { } named)
            return null;

        var parameter = new NameSyntax(TextOf(name.Span)) { Span = name.Span };
        return new(parameter, named) { Span = new(name.Span.Start, named.Span.End - name.Span.Start) };
    }

    /// <summary>
    /// Reads an argument's value. Operators need parentheses in arguments, so a value starting with <c>-</c> is reported
    /// with a suggestion to bracket it, and anything else that can't start a value is reported as unexpected.
    /// </summary>
    private ExpressionSyntax? ParseArgumentValue()
    {
        if (token.Kind is TokenKind.Minus)
        {
            int start = token.Span.Start;
            Advance();
            var negative = new TextSpan(start, token.Span.End - start);
            Fail(DiagnosticCatalog.NegativeArgument, negative, TextOf(negative));
            return null;
        }

        if (!StartsValue(token.Kind))
        {
            Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{TextOf(token.Span)}`");
            return null;
        }

        return ParsePrimary(argument: true);
    }

    private static bool StartsValue(TokenKind kind) =>
        kind is TokenKind.Name or TokenKind.Variable or TokenKind.Number or TokenKind.Duration or TokenKind.String or TokenKind.OpenParen;

    /// <summary>The <c>@</c> word a line starts with, read without lexing the line.</summary>
    private string AtWordOf(SourceLine line)
    {
        ReadOnlySpan<char> text = source.Text.AsSpan(line.Content.Start, line.Content.Length);
        int length = 1;
        while (length < text.Length && (char.IsLetterOrDigit(text[length]) || text[length] is '_'))
            length++;

        return text[..length].ToString();
    }
}
