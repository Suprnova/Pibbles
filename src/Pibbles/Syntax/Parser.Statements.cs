using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

internal sealed partial class Parser
{
    /// <summary>
    /// Parses the statement on <paramref name="line"/>, and any block it owns, into <paramref name="statements"/>. The line
    /// is usually the current one, but an alternative passes the part of its line after the <c>- </c>.
    /// </summary>
    private void ParseStatement(List<StatementSyntax> statements, SourceLine line)
    {
        if (line.Kind is LineKind.Option)
        {
            statements.Add(ParseChoice(line));
            return;
        }

        if (line.Kind is not LineKind.At)
        {
            statements.Add(ParseTextLine(line));
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
                while (token.Kind is TokenKind.Tag)
                    tags.Add(ReadTag());

                CheckTags(tags, TagPlace.Call);
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

            case "@sequence" or "@cycle" or "@once":
                statements.Add(ParseVariation(start, word));
                return;

            case var _ when DeclarationKeywords.Contains(word):
                Fail(DiagnosticCatalog.DeclarationInNode, token.Span);
                index++;
                SkipBlock();
                return;

            case var _ when ExtensionKeywords.Contains(word):
                Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{word}`");
                index++;
                SkipBlock();
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
        return ParseBlockUnder(keywordSpan, start, failed, lineEnd, () => ParseStatements(nodeLevel: false));
    }

    /// <summary>
    /// Parses the block under an opener whose line is finished, and returns it with where the statement ends. A missing
    /// block is reported, unless the opener's line already had a problem.
    /// </summary>
    private (List<T> Items, int End) ParseBlockUnder<T>(TextSpan keywordSpan, int start, bool openerFailed, int lineEnd, Func<List<T>> parseBlock)
        where T : SyntaxNode
    {
        if (Current.Kind is LineTokenKind.Indent)
        {
            index++;
            List<T> items = parseBlock();
            SkipDedent();
            return (items, items.Count > 0 ? Math.Max(items[^1].Span.End, lineEnd) : lineEnd);
        }

        if (!openerFailed)
            Report(DiagnosticCatalog.MissingBlock, keywordSpan, TextOf(new(start, lineEnd - start)));

        return ([], lineEnd);
    }

    /// <summary>Parses <c>@sequence:</c>, <c>@cycle:</c> or <c>@once:</c>, its tags, and its block.</summary>
    private StatementSyntax ParseVariation(int start, string word)
    {
        TextSpan keyword = token.Span;
        Advance();
        if (token.Kind is TokenKind.Colon)
            Advance();
        else
            Fail(DiagnosticCatalog.MissingColon, new(previousEnd, 0), word, TextOf(new(start, previousEnd - start)));

        List<TagSyntax> tags = [];
        while (token.Kind is TokenKind.Tag)
            tags.Add(ReadTag());

        CheckTags(tags, TagPlace.Variation);
        bool failed = lineFailed;
        int lineEnd = previousEnd;
        FinishLine();

        if (word is "@once")
        {
            (List<StatementSyntax> body, int onceEnd) = ParseBlockUnder(keyword, start, failed, lineEnd, () => ParseStatements(nodeLevel: false));
            return new OnceStatementSyntax(tags, body) { Span = new(start, onceEnd - start) };
        }

        (List<AlternativeSyntax> alternatives, int end) = ParseBlockUnder(keyword, start, failed, lineEnd, () => ParseAlternatives(word));
        VariationKind kind = word is "@sequence" ? VariationKind.Sequence : VariationKind.Cycle;
        return new VariationStatementSyntax(kind, tags, alternatives) { Span = new(start, end - start) };
    }

    /// <summary>
    /// Parses a variation's block, which holds only alternatives. The first line that isn't one is reported, and each such
    /// line becomes an alternative of its own, so its statements stay in the tree.
    /// </summary>
    private List<AlternativeSyntax> ParseAlternatives(string keyword)
    {
        List<AlternativeSyntax> alternatives = [];
        bool reported = false;
        while (!AtEndOfBlock)
        {
            List<StatementSyntax> stray = [];
            if (Current.Kind is LineTokenKind.Indent)
            {
                ReportUnexpectedIndentation();
                stray = ParseStatements(nodeLevel: false);
                SkipDedent();
            }
            else if (Current.Line.Kind is LineKind.Dash)
            {
                alternatives.Add(ParseAlternative(Current.Line));
                continue;
            }
            else if (Current.Line.Kind is LineKind.Header)
            {
                Report(DiagnosticCatalog.Unexpected, new(Current.Line.Content.Start, 2), "a node header");
                index++;
                SkipBlock();
            }
            else
            {
                if (!reported)
                    Report(DiagnosticCatalog.NotAlternative, new(Current.Line.Content.Start, TrimmedEnd(Current.Line) - Current.Line.Content.Start), keyword);

                reported = true;
                ParseStatement(stray, Current.Line);
            }

            if (stray.Count > 0)
                alternatives.Add(new(stray) { Span = new(stray[0].Span.Start, stray[^1].Span.End - stray[0].Span.Start) });
        }

        return alternatives;
    }

    /// <summary>
    /// Parses an alternative. After <c>- </c> comes a single-line statement, which the lines indented under it continue.
    /// A bare <c>-</c> leaves the whole alternative to the block under it. A block opener after <c>- </c> is reported, then
    /// parsed with its own block, so the tree stays complete.
    /// </summary>
    private AlternativeSyntax ParseAlternative(SourceLine line)
    {
        int start = line.Content.Start;
        int after = start + 1;
        while (after < line.Content.End && source.Text[after] is ' ' or '\t')
            after++;

        var content = new TextSpan(after, line.Content.End - after);
        var inner = new SourceLine(line.Number, LineClassifier.KindOf(source.Text.AsSpan(content.Start, content.Length)), content);
        List<StatementSyntax> body = [];

        if (inner.Kind is LineKind.Blank or LineKind.Comment or LineKind.Note)
        {
            index++;
            if (Current.Kind is LineTokenKind.Indent)
            {
                index++;
                body = ParseStatements(nodeLevel: false);
                SkipDedent();
            }
            else
            {
                Report(DiagnosticCatalog.MissingBlock, new(start, 1), "-");
            }
        }
        else if (inner.Kind is LineKind.Header)
        {
            Report(DiagnosticCatalog.Unexpected, new(after, 2), "a node header");
            index++;
            SkipBlock();
        }
        else
        {
            string opener = inner.Kind is LineKind.Option ? "->" : inner.Kind is LineKind.At ? AtWordOf(inner) : "";
            if (opener is "->" or "@if" or "@elif" or "@else" or "@sequence" or "@cycle" or "@once")
                Report(DiagnosticCatalog.OpenerInAlternative, new(after, opener.Length), opener);

            ParseStatement(body, inner);
            if (Current.Kind is LineTokenKind.Indent)
            {
                index++;
                body.AddRange(ParseStatements(nodeLevel: false));
                SkipDedent();
            }
        }

        int end = body.Count > 0 ? Math.Max(body[^1].Span.End, TrimmedEnd(line)) : TrimmedEnd(line);
        return new(body) { Span = new(start, end - start) };
    }

    private enum TagPlace
    {
        TextLine,
        PoseChange,
        Option,
        Call,
        Variation,
    }

    /// <summary>
    /// Checks where the reserved tags go. <c>#id</c> holds a line ID, at most once, and not on a pose change. <c>#was</c>
    /// only goes on a node header. A variation block takes no tag but <c>#id</c>.
    /// </summary>
    private void CheckTags(IReadOnlyList<TagSyntax> tags, TagPlace place)
    {
        bool hasId = false;
        foreach (TagSyntax tag in tags)
        {
            string text = TextOf(tag.Span);
            if (tag.Name is "was")
            {
                Fail(DiagnosticCatalog.TagNotAllowed, tag.Span, text, PlaceName(place), "`#was:` only goes on a node header, where it records the node's old names.");
            }
            else if (tag.Name is "id")
            {
                if (place is TagPlace.PoseChange)
                    Fail(DiagnosticCatalog.TagNotAllowed, tag.Span, text, PlaceName(place), "A pose change shows nothing, so a save never stops on it. Remove the `#id`.");
                else if (hasId)
                    Fail(DiagnosticCatalog.TagNotAllowed, tag.Span, text, "a line that already has an `#id`", "A line has one ID. Remove one of them.");
                else if (!IsLineId(tag.Value))
                    Fail(DiagnosticCatalog.InvalidLineId, tag.Span, text);

                hasId = true;
            }
            else if (place is TagPlace.Variation)
            {
                Fail(DiagnosticCatalog.TagNotAllowed, tag.Span, text, PlaceName(place), "A variation block only takes an `#id:` tag. Put other tags on the lines inside it.");
            }
        }
    }

    private static string PlaceName(TagPlace place) => place switch
    {
        TagPlace.TextLine => "a text line",
        TagPlace.PoseChange => "a line that only changes a pose",
        TagPlace.Option => "an option",
        TagPlace.Call => "`@call`",
        _ => "a variation block",
    };

    /// <summary>Whether a value is a line ID: a lowercase ASCII letter, then lowercase letters, digits and <c>_</c>.</summary>
    private static bool IsLineId(string? value) =>
        value is [>= 'a' and <= 'z', ..] && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

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

        List<ArgumentSyntax> arguments = ParseArguments(allowWait: true, out CommandWait wait);
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
