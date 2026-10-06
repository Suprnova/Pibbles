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
internal sealed partial class Parser
{
    private readonly SourceText source;
    private readonly IReadOnlyList<LineToken> lines;
    private readonly List<Diagnostic> diagnostics;
    private readonly List<Comment> comments;
    private readonly List<DeclarationSyntax> declarations = [];
    private readonly List<NodeSyntax> nodes = [];
    private PrefixSyntax? prefix;
    private bool sawContent;
    private int index;

    private CodeLexer lexer = null!;
    private Token token;
    private int previousEnd;
    private TextSpan previousSpan;
    private bool lineFailed;

    private Parser(SourceText source)
    {
        this.source = source;
        ClassifiedLines classified = LineClassifier.Classify(source);
        lines = classified.Tokens;
        comments = [.. classified.Comments];
        diagnostics = [.. classified.Diagnostics];
    }

    public static SyntaxTree Parse(SourceText source) => new Parser(source).ParseTree();

    private LineToken Current => lines[index];

    private bool AtEndOfBlock => Current.Kind is LineTokenKind.Dedent || Current is { Kind: LineTokenKind.Line, Line.Kind: LineKind.EndOfFile };

    private SyntaxTree ParseTree()
    {
        ParseTopLevel();
        var root = new FileSyntax(prefix, declarations, nodes) { Span = new(0, source.Text.Length) };
        return new(source, root, [.. comments.DistinctBy(comment => comment.Span).OrderBy(comment => comment.Span.Start)], diagnostics);
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
            else if (nodes.Count > 0)
            {
                ContinueLastNode();
            }
            else
            {
                ParseTopLevelLine(Current.Line);
            }
        }
    }

    /// <summary>
    /// Adds the lines after a node's body to that node. A body only ends before the next header when its header was
    /// indented and the lines below it aren't, and those lines still come after the node.
    /// </summary>
    private void ContinueLastNode()
    {
        List<StatementSyntax> statements = ParseStatements(nodeLevel: true);
        if (statements.Count == 0)
            return;

        NodeSyntax node = nodes[^1];
        nodes[^1] = node with { Body = [.. node.Body, .. statements], Span = new(node.Span.Start, statements[^1].Span.End - node.Span.Start) };
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
                declarations.Add(ParseDeclaration(line));
                return;
            }

            if (ExtensionKeywords.Contains(word))
            {
                Fail(DiagnosticCatalog.Unexpected, token.Span, $"`{word}`");
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
        while (token.Kind is TokenKind.Tag)
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
            else
            {
                ParseStatement(statements, Current.Line);
            }
        }

        return statements;
    }

    private void ReportUnexpectedIndentation()
    {
        Report(DiagnosticCatalog.UnexpectedIndentation, IndentationOf(Current.Line));
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
        lexer = new(source, content, diagnostics, comments);
        lineFailed = false;
        previousEnd = content.Start;
        previousSpan = new(content.Start, 0);
        token = lexer.Next();
    }

    private void Advance()
    {
        previousSpan = token.Span;
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
        if (token.Kind is TokenKind.Name)
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
        TagSyntax tag = TagFrom(token);
        Advance();
        return tag;
    }

    private TagSyntax TagFrom(Token tag)
    {
        string text = TextOf(tag.Span);
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new TagSyntax(text[1..], null) { Span = tag.Span }
            : new TagSyntax(text[1..colon], text[(colon + 1)..]) { Span = tag.Span };
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
