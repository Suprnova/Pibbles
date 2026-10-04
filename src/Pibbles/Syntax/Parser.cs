using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// Builds a file's syntax tree from the line classifier's tokens, reading each line with the lexer.
/// It follows the syntax layer of the grammar in <c>docs/language/reference.md</c>.
/// </summary>
/// <remarks>
/// After the first problem in a line, the parser reports it and skips the rest of that line, so a line gets at most one
/// diagnostic from the parser. An indented block where none is allowed is reported once, and its lines join the block
/// around it.
/// </remarks>
internal sealed class Parser
{
    private static readonly HashSet<string> DeclarationKeywords = ["@actor", "@enum", "@var", "@command", "@markup", "@icon", "@tag", "@function"];

    private readonly SourceText source;
    private readonly IReadOnlyList<LineToken> lines;
    private readonly List<Diagnostic> diagnostics;
    private readonly List<DeclarationSyntax> declarations = [];
    private readonly List<NodeSyntax> nodes = [];
    private PrefixSyntax? prefix;
    private bool sawContent;
    private int index;

    private CodeLexer lexer = null!;
    private Token token;
    private int previousEnd;
    private bool lineFailed;

    private Parser(SourceText source)
    {
        this.source = source;
        ClassifiedLines classified = LineClassifier.Classify(source);
        lines = classified.Tokens;
        diagnostics = [.. classified.Diagnostics];
    }

    public static SyntaxTree Parse(SourceText source) => new Parser(source).ParseTree();

    private LineToken Current => lines[index];

    private bool AtEndOfBlock => Current.Kind is LineTokenKind.Dedent || Current is { Kind: LineTokenKind.Line, Line.Kind: LineKind.EndOfFile };

    private SyntaxTree ParseTree()
    {
        ParseTopLevel();
        var root = new FileSyntax(prefix, declarations, nodes) { Span = new(0, source.Text.Length) };
        return new(source, root, diagnostics);
    }

    private void ParseTopLevel()
    {
        while (!AtEndOfBlock)
        {
            if (Current.Kind is LineTokenKind.Indent)
            {
                ReportUnexpectedIndentation();
                ParseTopLevel();
                SkipDedent();
            }
            else if (Current.Line.Kind is LineKind.Header)
            {
                sawContent = true;
                nodes.Add(ParseNode());
            }
            else
            {
                ParseTopLevelLine(Current.Line);
            }
        }
    }

    private void ParseTopLevelLine(SourceLine line)
    {
        bool first = !sawContent;
        sawContent = true;

        if (line.Kind is LineKind.At)
        {
            StartLine(line.Content);
            string word = TextOf(token.Span);
            if (word is "@prefix")
            {
                ParsePrefix(line, first);
                return;
            }

            if (DeclarationKeywords.Contains(word))
            {
                declarations.Add(new UnparsedDeclarationSyntax { Span = line.Content });
                SkipRestOfLine();
                index++;
                SkipBlock();
                return;
            }
        }

        Report(DiagnosticCatalog.OutsideNode, line.Content);
        index++;
        SkipBlock();
    }

    private void ParsePrefix(SourceLine line, bool first)
    {
        if (!first || prefix is not null)
        {
            ReportMisplacedPrefix();
            return;
        }

        Advance();
        NameSyntax name = ExpectName("a node name", "@prefix");
        prefix = new(name) { Span = SpanFrom(line.Content.Start) };
        FinishLine();
    }

    /// <summary>Reports a <c>@prefix</c> that isn't the file's first line of content, and skips it.</summary>
    private void ReportMisplacedPrefix()
    {
        if (prefix is null)
            Fail(DiagnosticCatalog.MisplacedPrefix, token.Span);
        else
            Fail(DiagnosticCatalog.DuplicatePrefix, token.Span, source.GetLinePosition(prefix.Span.Start).Line + 1);

        index++;
    }

    private NodeSyntax ParseNode()
    {
        SourceLine header = Current.Line;
        StartLine(new(header.Content.Start + 2, header.Content.Length - 2));
        previousEnd = header.Content.Start + 2;

        NameSyntax name = ExpectName("a node name", "==");
        List<NameSyntax> aliases = [];
        while (token.Kind is TokenKind.Tag && !lineFailed)
        {
            TagSyntax tag = ReadTag();
            if (tag.Name is not "was")
                Fail(DiagnosticCatalog.TagNotAllowed, tag.Span, TextOf(tag.Span), "a node header", "A node header only takes `#was:` tags. Put other tags on the lines inside the node.");
            else if (ParseAlias(tag) is { } alias)
                aliases.Add(alias);
        }

        int headerEnd = previousEnd;
        FinishLine();

        List<StatementSyntax> body = ParseStatements(nodeLevel: true);
        int end = body.Count > 0 ? body[^1].Span.End : headerEnd;
        return new(name, aliases, body) { Span = new(header.Content.Start, end - header.Content.Start) };
    }

    /// <summary>Reads a <c>#was:</c> tag's value as a node name, which must fill the whole value.</summary>
    private NameSyntax? ParseAlias(TagSyntax tag)
    {
        var value = new TextSpan(tag.Span.Start + "#was:".Length, Math.Max(tag.Span.Length - "#was:".Length, 0));
        Token name = new CodeLexer(source, value, []).Next();
        if (tag.Value is not null && name.Kind is TokenKind.Name && name.Span == value)
            return new(tag.Value) { Span = value };

        Fail(DiagnosticCatalog.Missing, tag.Value is null ? new(tag.Span.End, 0) : value, "a node name", "#was:");
        return null;
    }

    private List<StatementSyntax> ParseStatements(bool nodeLevel)
    {
        List<StatementSyntax> statements = [];
        while (!AtEndOfBlock)
        {
            if (Current.Kind is LineTokenKind.Indent)
            {
                ReportUnexpectedIndentation();
                statements.AddRange(ParseStatements(nodeLevel: false));
                SkipDedent();
            }
            else if (Current.Line.Kind is LineKind.Header)
            {
                if (nodeLevel)
                    break;

                Report(DiagnosticCatalog.Unexpected, new(Current.Line.Content.Start, 2), "a node header");
                index++;
                SkipBlock();
            }
            else if (ParseStatement() is { } statement)
            {
                statements.Add(statement);
            }
        }

        return statements;
    }

    private StatementSyntax? ParseStatement()
    {
        SourceLine line = Current.Line;
        if (line.Kind is not LineKind.At)
            return ParseUnparsedStatement(line);

        StartLine(line.Content);
        switch (TextOf(token.Span))
        {
            case "@jump":
                Advance();
                NameSyntax destination = ExpectName("a node name", "@jump");
                return FinishStatement(new JumpStatementSyntax(destination) { Span = SpanFrom(line.Content.Start) });

            case "@call":
                Advance();
                NameSyntax target = ExpectName("a node name", "@call");
                List<TagSyntax> tags = [];
                while (token.Kind is TokenKind.Tag && !lineFailed)
                    tags.Add(ReadTag());

                return FinishStatement(new CallStatementSyntax(target, tags) { Span = SpanFrom(line.Content.Start) });

            case "@return":
                Advance();
                return FinishStatement(new ReturnStatementSyntax { Span = SpanFrom(line.Content.Start) });

            case "@end":
                Advance();
                return FinishStatement(new EndStatementSyntax { Span = SpanFrom(line.Content.Start) });

            case "@prefix":
                ReportMisplacedPrefix();
                return null;

            default:
                SkipRestOfLine();
                return ParseUnparsedStatement(line);
        }
    }

    private StatementSyntax FinishStatement(StatementSyntax statement)
    {
        FinishLine();
        return statement;
    }

    /// <summary>Consumes a line the parser doesn't read yet, and parses the block under it as its body.</summary>
    private UnparsedStatementSyntax ParseUnparsedStatement(SourceLine line)
    {
        index++;
        List<StatementSyntax> body = [];
        if (Current.Kind is LineTokenKind.Indent)
        {
            index++;
            body = ParseStatements(nodeLevel: false);
            SkipDedent();
        }

        int end = body.Count > 0 ? Math.Max(body[^1].Span.End, line.Content.End) : line.Content.End;
        return new(body) { Span = new(line.Content.Start, end - line.Content.Start) };
    }

    private void ReportUnexpectedIndentation()
    {
        SourceLine line = Current.Line;
        int start = source.GetLineSpan(line.Number).Start;
        Report(DiagnosticCatalog.UnexpectedIndentation, new(start, line.Content.Start - start));
        index++;
    }

    private void SkipDedent()
    {
        if (Current.Kind is LineTokenKind.Dedent)
            index++;
    }

    private void SkipBlock()
    {
        if (Current.Kind is not LineTokenKind.Indent)
            return;

        for (int depth = 0; ; index++)
        {
            depth += Current.Kind switch
            {
                LineTokenKind.Indent => 1,
                LineTokenKind.Dedent => -1,
                _ => 0,
            };

            if (depth == 0)
            {
                index++;
                return;
            }
        }
    }

    private void StartLine(TextSpan content)
    {
        lexer = new(source, content, diagnostics);
        lineFailed = false;
        previousEnd = content.Start;
        token = lexer.Next();
    }

    private void Advance()
    {
        previousEnd = token.Span.End;
        token = lexer.Next();
    }

    private bool AtLineEnd => token.Kind is TokenKind.EndOfLine or TokenKind.Comment;

    /// <summary>Reports anything left on the line, unless the line already failed, and moves to the next line.</summary>
    private void FinishLine()
    {
        if (!AtLineEnd && !lineFailed)
            Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{TextOf(token.Span)}`");

        index++;
    }

    /// <summary>Reads the rest of the line, so the lexer reports any malformed tokens in it.</summary>
    private void SkipRestOfLine()
    {
        while (token.Kind is not TokenKind.EndOfLine)
            Advance();
    }

    private NameSyntax ExpectName(string what, string after)
    {
        if (token.Kind is TokenKind.Name && !lineFailed)
        {
            var name = new NameSyntax(TextOf(token.Span)) { Span = token.Span };
            Advance();
            return name;
        }

        Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), what, after);
        return new("") { Span = new(previousEnd, 0) };
    }

    private TagSyntax ReadTag()
    {
        string text = TextOf(token.Span);
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        var tag = colon < 0
            ? new TagSyntax(text[1..], null) { Span = token.Span }
            : new TagSyntax(text[1..colon], text[(colon + 1)..]) { Span = token.Span };

        Advance();
        return tag;
    }

    private TextSpan SpanFrom(int start) => new(start, previousEnd - start);

    private string TextOf(TextSpan span) => source.Text.Substring(span.Start, span.Length);

    /// <summary>Reports the line's first problem. Later problems on the same line are dropped.</summary>
    private void Fail(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments)
    {
        if (lineFailed)
            return;

        lineFailed = true;
        Report(descriptor, span, arguments);
    }

    private void Report(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments) =>
        diagnostics.Add(descriptor.Create(source.GetLocation(span), arguments));
}
