using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>Declarations: the lines before a file's first node that make up the contract between the story and the host.</summary>
internal sealed partial class Parser
{
    private static readonly HashSet<string> DeclarationKeywords = ["@actor", "@enum", "@var", "@command", "@markup", "@icon", "@tag", "@function"];

    /// <summary>Keywords that grammar extensions will use. v1 gives them no meaning, and rejects them where they'd appear.</summary>
    private static readonly HashSet<string> ExtensionKeywords = ["@term", "@resume", "@shuffle"];

    /// <summary>Parses the declaration on the current line, whose keyword is the current token.</summary>
    private DeclarationSyntax ParseDeclaration(SourceLine line)
    {
        int start = line.Content.Start;
        TextSpan keyword = token.Span;
        string word = TextOf(keyword);
        Advance();

        if (word is "@actor")
            return ParseActor(start, keyword);

        DeclarationSyntax declaration = word switch
        {
            "@enum" => ParseEnum(),
            "@var" => ParseVariableDeclaration(),
            "@command" => ParseCommandDeclaration(),
            "@markup" => ParseMarkupDeclaration(),
            "@icon" => new IconDeclarationSyntax(ParseNameList("an icon name", "@icon")) { Span = default },
            "@tag" => ParseTagDeclaration(),
            _ => ParseFunctionDeclaration(),
        };

        declaration = declaration with { Span = SpanFrom(start) };
        FinishLine();
        return declaration;
    }

    private ActorDeclarationSyntax ParseActor(int start, TextSpan keyword)
    {
        NameSyntax name = ExpectDeclaredName("an actor name", "@actor");
        RejectColon("@actor", start);

        bool failed = lineFailed;
        int end = previousEnd;
        FinishLine();

        string? displayName = null;
        TextSpan? displayNameSpan = null;
        List<NameSyntax> poses = [];
        if (Current.Kind is LineTokenKind.Indent)
        {
            index++;
            while (!AtEndOfBlock)
            {
                if (Current.Kind is LineTokenKind.Indent)
                {
                    Report(DiagnosticCatalog.UnexpectedIndentation, IndentationOf(Current.Line));
                    SkipBlock();
                    continue;
                }

                end = TrimmedEnd(Current.Line);
                ParseActorProperty(Current.Line, ref displayName, ref displayNameSpan, poses);
            }

            SkipDedent();
        }
        else if (!failed)
        {
            Report(DiagnosticCatalog.MissingBlock, keyword, TextOf(new(start, end - start)));
        }

        return new(name, displayName, displayNameSpan, poses) { Span = new(start, end - start) };
    }

    /// <summary>Parses one line of an actor's block: <c>name: Display name</c>, read as plain text, or <c>poses: a, b, c</c>.</summary>
    private void ParseActorProperty(SourceLine line, ref string? displayName, ref TextSpan? displayNameSpan, List<NameSyntax> poses)
    {
        lineFailed = false;
        inlineEnd = line.Content.End;
        int start = line.Content.Start;
        int wordEnd = SkipIdentifier(start);
        string word = source.Text[start..wordEnd];
        int colon = SkipWhitespace(wordEnd);

        if (line.Kind is not LineKind.Text || word is not ("name" or "poses") || colon >= inlineEnd || source.Text[colon] is not ':')
        {
            var unexpected = new TextSpan(start, Math.Max(wordEnd, start + 1) - start);
            Fail(DiagnosticCatalog.Unexpected, unexpected, $"`{TextOf(unexpected)}`");
            index++;
            SkipBlock();
            return;
        }

        if (word is "poses")
        {
            StartLine(new(colon + 1, line.Content.End - colon - 1));
            previousSpan = new(colon, 1);
            previousEnd = colon + 1;
            poses.AddRange(ParseNameList("a pose", "poses:"));
            FinishLine();
            return;
        }

        int valueStart = SkipWhitespace(colon + 1);
        var value = new TextSpan(valueStart, TrimmedEnd(line) - valueStart);
        if (value.Length == 0)
            Fail(DiagnosticCatalog.Missing, new(colon + 1, 0), "a display name", "name:");
        else if (TextOf(value).AsSpan().IndexOfAny("[{\\") >= 0)
            Fail(DiagnosticCatalog.InvalidDisplayName, value);

        displayName = TextOf(value);
        displayNameSpan = value;
        index++;
    }

    private EnumDeclarationSyntax ParseEnum()
    {
        NameSyntax name = ExpectDeclaredName("an enum name", "@enum");
        ExpectToken(TokenKind.Colon, ":");
        return new(name, ParseNameList("an enum member", ":")) { Span = default };
    }

    private VariableDeclarationSyntax ParseVariableDeclaration()
    {
        VariableExpressionSyntax variable;
        if (token.Kind is TokenKind.Variable)
        {
            variable = new(TextOf(token.Span)[1..]) { Span = token.Span };
            Advance();
        }
        else
        {
            Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "a variable", "@var");
            variable = new("") { Span = new(previousEnd, 0) };
        }

        NameSyntax? type = null;
        if (token.Kind is TokenKind.Colon)
        {
            Advance();
            type = ExpectDeclaredName("a type", ":");
        }

        ExpectToken(TokenKind.Equals, "=");
        return new(variable, type, ParseConstant()) { Span = default };
    }

    private CommandDeclarationSyntax ParseCommandDeclaration()
    {
        NameSyntax name = ExpectDeclaredName("a command name", "@command");
        List<ParameterSyntax> parameters = ParseParameterList(name);

        bool isInline = false;
        bool waits = false;
        while (token.Kind is TokenKind.Name && TextOf(token.Span) is "inline" or "waits")
        {
            isInline |= TextOf(token.Span) is "inline";
            waits |= TextOf(token.Span) is "waits";
            Advance();
        }

        return new(name, parameters, isInline, waits) { Span = default };
    }

    private MarkupDeclarationSyntax ParseMarkupDeclaration()
    {
        NameSyntax name = ExpectDeclaredName("a markup name", "@markup");
        List<ParameterSyntax> parameters = token.Kind is TokenKind.CallOpen or TokenKind.OpenParen ? ParseParameterList(name) : [];
        return new(name, parameters) { Span = default };
    }

    private TagDeclarationSyntax ParseTagDeclaration()
    {
        List<TagEntrySyntax> entries = [];
        string after = "@tag";
        while (true)
        {
            NameSyntax name = ExpectDeclaredName("a tag name", after);
            NameSyntax? type = null;
            bool allowsEmpty = false;
            if (token.Kind is TokenKind.Colon)
            {
                Advance();
                type = ExpectDeclaredName("a type", ":");
                if (token.Kind is TokenKind.Question)
                {
                    Advance();
                    allowsEmpty = true;
                }
            }

            entries.Add(new(name, type, allowsEmpty) { Span = new(name.Span.Start, previousEnd - name.Span.Start) });
            if (token.Kind is not TokenKind.Comma)
                break;

            Advance();
            after = ",";
        }

        return new(entries) { Span = default };
    }

    private FunctionDeclarationSyntax ParseFunctionDeclaration()
    {
        NameSyntax name = ExpectDeclaredName("a function name", "@function");
        List<ParameterSyntax> parameters = ParseParameterList(name);
        ExpectToken(TokenKind.Arrow, "->");
        return new(name, parameters, ExpectDeclaredName("a type", "->")) { Span = default };
    }

    /// <summary>Parses <c>(name: type [= default], …)</c>.</summary>
    private List<ParameterSyntax> ParseParameterList(NameSyntax owner)
    {
        List<ParameterSyntax> parameters = [];
        if (token.Kind is not (TokenKind.CallOpen or TokenKind.OpenParen))
        {
            Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "`(`", owner.IsMissing ? TextOf(previousSpan) : owner.Text);
            return parameters;
        }

        TextSpan open = token.Span;
        Advance();
        if (token.Kind is TokenKind.CloseParen)
        {
            Advance();
            return parameters;
        }

        string after = "(";
        while (true)
        {
            NameSyntax name = ExpectDeclaredName("a parameter name", after);
            ExpectToken(TokenKind.Colon, ":");
            NameSyntax type = ExpectDeclaredName("a type", ":");
            ExpressionSyntax? @default = null;
            if (token.Kind is TokenKind.Equals)
            {
                Advance();
                @default = ParseConstant();
            }

            parameters.Add(new(name, type, @default) { Span = new(name.Span.Start, previousEnd - name.Span.Start) });
            if (token.Kind is not TokenKind.Comma)
                break;

            Advance();
            after = ",";
        }

        ExpectCloseParen(open);
        return parameters;
    }

    /// <summary>Parses a constant: a literal, a negative number or duration, or a bare name such as an enum member or a node.</summary>
    private ExpressionSyntax ParseConstant()
    {
        if (token.Kind is TokenKind.Minus)
        {
            TextSpan minus = token.Span;
            Advance();
            if (token.Kind is not (TokenKind.Number or TokenKind.Duration))
                return MissingValue();

            ExpressionSyntax operand = ParsePrimary(argument: true);
            return new UnaryExpressionSyntax(UnaryOperator.Negate, minus, operand) { Span = new(minus.Start, operand.Span.End - minus.Start) };
        }

        if (token.Kind is TokenKind.Number or TokenKind.Duration or TokenKind.String)
            return ParsePrimary(argument: true);

        if (token.Kind is not TokenKind.Name)
        {
            if (AtLineEnd)
                return MissingValue();

            Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{TextOf(token.Span)}`");
            return new ErrorExpressionSyntax { Span = token.Span };
        }

        Token name = token;
        Advance();
        string text = TextOf(name.Span);
        return text is "true" or "false"
            ? new BooleanLiteralSyntax(text is "true") { Span = name.Span }
            : new NameExpressionSyntax(text) { Span = name.Span };
    }

    private List<NameSyntax> ParseNameList(string what, string after)
    {
        List<NameSyntax> names = [ExpectDeclaredName(what, after)];
        while (token.Kind is TokenKind.Comma)
        {
            Advance();
            names.Add(ExpectDeclaredName(what, ","));
        }

        return names;
    }

    /// <summary>Reads a name being declared, which is a single identifier: a dotted or relative name is reported.</summary>
    private NameSyntax ExpectDeclaredName(string what, string after)
    {
        NameSyntax name = ExpectName(what, after);
        if (name.Text.Contains('.', StringComparison.Ordinal))
            Fail(DiagnosticCatalog.DottedName, name.Span, name.Text);

        return name;
    }

    private void ExpectToken(TokenKind kind, string text)
    {
        if (token.Kind == kind)
            Advance();
        else
            Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), $"`{text}`", TextOf(previousSpan));
    }

    private TextSpan IndentationOf(SourceLine line)
    {
        int start = source.GetLineSpan(line.Number).Start;
        return new(start, line.Content.Start - start);
    }
}
