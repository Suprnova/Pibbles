using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>Declarations: the lines before a file's first node that make up the contract between the story and the host.</summary>
internal sealed partial class Parser
{
    private static readonly HashSet<string> DeclarationKeywords = ["@actor", "@channel", "@narration", "@enum", "@var", "@command", "@markup", "@icon", "@tag", "@function"];

    /// <summary>Keywords that grammar extensions will use. v1 gives them no meaning, and rejects them where they'd appear.</summary>
    private static readonly HashSet<string> ExtensionKeywords = ["@term", "@resume", "@shuffle", "@temp"];

    /// <summary>Parses the declaration on the current line, whose keyword is the current token.</summary>
    private DeclarationSyntax ParseDeclaration(SourceLine line)
    {
        int start = line.Content.Start;
        TextSpan keyword = token.Span;
        string word = CodeOf(keyword);
        Advance();

        if (word is "@actor")
            return ParseActor(start, keyword);

        if (word is "@narration")
            return ParseNarration(start, keyword);

        DeclarationSyntax declaration = word switch
        {
            "@channel" => ParseChannel(),
            "@enum" => ParseEnum(),
            "@var" => ParseVariableDeclaration(line),
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
        (string? displayName, List<ChannelEditSyntax> channels, int end) = ParseSpeakerBlock(start, keyword);
        return new(name, displayName, channels) { Span = new(start, end - start) };
    }

    private NarrationDeclarationSyntax ParseNarration(int start, TextSpan keyword)
    {
        RejectColon("@narration", start);
        (string? displayName, List<ChannelEditSyntax> channels, int end) = ParseSpeakerBlock(start, keyword);
        return new(displayName, channels) { Span = new(start, end - start) };
    }

    /// <summary>Finishes the line of an actor or narration and parses the block under it, which has the same lines for both.</summary>
    private (string? DisplayName, List<ChannelEditSyntax> Channels, int End) ParseSpeakerBlock(int start, TextSpan keyword)
    {
        int end = previousEnd;
        FinishLine();
        bool failed = lineFailed;

        string? displayName = null;
        List<ChannelEditSyntax> channels = [];
        if (Current.Kind is LineTokenKind.Indent)
        {
            index++;
            end = ParseSpeakerLines(ref displayName, channels, end);
            SkipDedent();
        }
        else if (!failed)
        {
            Report(DiagnosticCatalog.MissingBlock, keyword, TextOf(new(start, end - start)));
        }

        return (displayName, channels, end);
    }

    /// <summary>
    /// Parses the lines of an actor's or narration's block, and returns where the last one ends. An over-indented line is
    /// reported, and the lines indented with it are read as if they weren't.
    /// </summary>
    private int ParseSpeakerLines(ref string? displayName, List<ChannelEditSyntax> channels, int end)
    {
        while (!AtEndOfBlock)
        {
            if (Current.Kind is LineTokenKind.Indent)
            {
                ReportUnexpectedIndentation();
                end = ParseSpeakerLines(ref displayName, channels, end);
                SkipDedent();
                continue;
            }

            end = ParseActorProperty(Current.Line, ref displayName, channels);
        }

        return end;
    }

    /// <summary>
    /// Parses one line of an actor's or narration's block: <c>name: Display name</c>, read as plain text; <c>pose: a, b</c> or
    /// <c>pose += c</c> for a channel's values; or <c>no camera, effect</c> for the channels the actor doesn't have.
    /// Returns where what it read ends: the whole line, before its comment, when the line is none of those.
    /// </summary>
    private int ParseActorProperty(SourceLine line, ref string? displayName, List<ChannelEditSyntax> channels)
    {
        lineFailed = false;
        inlineEnd = TrimmedEnd(line);
        int start = line.Content.Start;
        int wordEnd = SkipIdentifier(start);
        string word = source.Text[start..wordEnd];
        int next = SkipWhitespace(wordEnd);
        bool colon = next < inlineEnd && code[next] is ':';
        bool plusEquals = next + 1 < inlineEnd && code.AsSpan(next, 2) is "+=";

        if (line.Kind is not LineKind.Text || wordEnd == start || !(colon || plusEquals || (word is "no" && next > wordEnd && SkipIdentifier(next) > next)))
        {
            int unexpectedEnd = line.Kind switch
            {
                LineKind.At => Identifiers.Skip(source.Text, start + 1, inlineEnd),
                LineKind.Option or LineKind.Header => start + 2,
                _ => Math.Max(wordEnd, start + 1),
            };
            var unexpected = new TextSpan(start, unexpectedEnd - start);
            FailUnexpected(unexpected);
            index++;
            SkipBlock();
            return PeekContentEnd(line);
        }

        if (word is "name" && colon)
        {
            if (code[next] != source.Text[next])
                forms.Record(next, ':');

            if (displayName is not null)
                Fail(DiagnosticCatalog.RepeatedDisplayName, new(start, wordEnd - start));

            int valueStart = SkipWhitespace(next + 1);
            var value = new TextSpan(valueStart, PlainContentEnd(valueStart, inlineEnd) - valueStart);
            if (value.Length == 0)
                Fail(DiagnosticCatalog.Missing, new(next + 1, 0), "a display name", "name:");

            displayName ??= TextOf(value);
            index++;
            return value.End;
        }

        StartLine(line.Content);
        if (word is "no" && !colon && !plusEquals)
        {
            Advance();
            channels.AddRange(ParseNameList("a channel", "no").Select(channel => new ChannelEditSyntax(channel, ChannelEdit.Drop, []) { Span = channel.Span }));
        }
        else
        {
            NameSyntax channel = ExpectName("a channel", "");
            ChannelEdit edit = plusEquals ? ChannelEdit.Add : ChannelEdit.Replace;
            string separator = plusEquals ? "+=" : ":";
            Advance();
            channels.Add(new(channel, edit, ParseNameList("a value", separator)) { Span = SpanFrom(start) });
        }

        int end = previousEnd;
        FinishLine();
        return end;
    }

    private int SkipIdentifier(int start) => Identifiers.Skip(source.Text, start, inlineEnd);

    private ChannelDeclarationSyntax ParseChannel()
    {
        NameSyntax name = ExpectDeclaredName("a channel name", "@channel");
        List<NameSyntax> values = [];
        if (token.Kind is TokenKind.Colon)
        {
            Advance();
            values = ParseNameList("a value", ":");
        }

        return new(name, values) { Span = default };
    }

    private EnumDeclarationSyntax ParseEnum()
    {
        NameSyntax name = ExpectDeclaredName("an enum name", "@enum");
        ExpectToken(TokenKind.Colon, ":");
        return new(name, ParseNameList("an enum member", ":")) { Span = default };
    }

    private VariableDeclarationSyntax ParseVariableDeclaration(SourceLine line)
    {
        VariableExpressionSyntax variable = ExpectVariable("@var", line);
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
            TagValueSyntax? value = token.Kind is TokenKind.Colon ? ParseTagValue() : null;
            entries.Add(new(name, value) { Span = new(name.Span.Start, previousEnd - name.Span.Start) });
            if (token.Kind is not TokenKind.Comma)
                break;

            Advance();
            after = ",";
        }

        return new(entries) { Span = default };
    }

    private TagValueSyntax ParseTagValue()
    {
        int start = token.Span.Start;
        Advance();
        NameSyntax type = ExpectDeclaredName("a type", ":");
        bool allowsEmpty = token.Kind is TokenKind.Question;
        if (allowsEmpty)
            Advance();

        return new(type, allowsEmpty) { Span = new(start, previousEnd - start) };
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

    /// <summary>
    /// Parses a constant: a literal, a negative number or duration, or a bare name such as an enum member or a node.
    /// An operator word (<c>and</c>, <c>or</c> or <c>not</c>) is none of these, so it's reported and read as an error.
    /// </summary>
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

        bool operatorWord = AtOperatorWord;
        if (token.Kind is not TokenKind.Name || operatorWord)
        {
            if (AtLineEnd)
                return MissingValue();

            FailUnexpected(token.Span, operatorWord ? $"`{TextOf(token.Span)}` is an operator, so it can't be a value here. Use a literal, or the name of an enum member, actor or node." : null);
            var error = new ErrorExpressionSyntax { Span = token.Span };
            Advance();
            return error;
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
}
