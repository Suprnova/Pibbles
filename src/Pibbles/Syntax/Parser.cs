using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// Builds a file's syntax tree from the line classifier's tokens, reading each line with the lexer.
/// It follows the syntax layer of the grammar in <c>docs/language/reference.md</c>.
/// </summary>
/// <remarks>
/// After the first problem in a line, the parser reports it and skips the rest of that line, so a line gets at most one
/// diagnostic from the parser, and none once the lexer has reported one of its tokens or the line has a note. An indented
/// block where none is allowed is reported once, and its lines join the block around it. A marker typed through an input
/// method is read as the marker and reported once parsing is done, so it never stops the rest of its line from being checked.
/// </remarks>
internal sealed partial class Parser
{
    /// <summary>
    /// The most levels the parser nests: indented blocks, markup spans, conditional-text branches, parentheses, call
    /// arguments, <c>not</c> and <c>-</c> operators, and chained operators. Past it the parser reports PIB1062 and builds
    /// nothing deeper, so every pass that walks the tree recurses a bounded number of times.
    /// </summary>
    internal const int MaxNesting = 64;

    private readonly SourceText source;
    private readonly IReadOnlyList<LineToken> lines;
    private readonly List<Diagnostic> diagnostics;
    private readonly List<Comment> comments;
    private readonly List<SyntaxNode> members = [];
    private PrefixSyntax? prefix;
    private bool sawContent;
    private bool sawNode;
    private int index;
    private int nesting;

    private readonly string code;
    private readonly MarkerFormLog forms = new();
    private readonly CodeLexer lexer;
    private readonly CodeLexer scratch;
    private Token token;
    private int previousEnd;
    private TextSpan previousSpan;
    private bool lineFailed;
    private TextSpan? reportedLine;

    private Parser(SourceText source)
    {
        this.source = source;
        ClassifiedLines classified = LineClassifier.Classify(source, forms);
        lines = classified.Tokens;
        comments = [.. classified.Comments];
        diagnostics = [.. classified.Diagnostics];
        code = MarkerForms.Map(source.Text);
        lexer = new(source, diagnostics, forms, code);
        scratch = new(source, diagnostics: null, code: code);
    }

    public static SyntaxTree Parse(SourceText source) => new Parser(source).ParseTree();

    private LineToken Current => lines[index];

    private bool AtEndOfBlock => Current.Kind is LineTokenKind.Dedent || Current is { Kind: LineTokenKind.Line, Line.Kind: LineKind.EndOfFile };

    private SyntaxTree ParseTree()
    {
        ParseTopLevel();
        var root = new FileSyntax(prefix, members) { Span = new(0, source.Text.Length) };
        diagnostics.AddRange(forms.Report(source));
        return new(source, root, [.. comments.OrderBy(comment => comment.Span.Start)], diagnostics);
    }

    private void ParseTopLevel()
    {
        while (!AtEndOfBlock)
        {
            if (Current.Kind is LineTokenKind.Indent)
            {
                ReportUnexpectedIndentation();
                if (TryEnterBlock())
                {
                    ParseTopLevel();
                    nesting--;
                }

                SkipDedent();
            }
            else if (Current.Line.Kind is LineKind.Header)
            {
                sawContent = true;
                sawNode = true;
                members.Add(ParseNode());
            }
            else if (sawNode && !AtVariablesBeforeHeader())
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
    /// Adds the lines after a node's body to that node. A body ends before the next header, or before the variable
    /// declarations directly above it, except when its header was indented and the lines below it aren't. Those lines
    /// still belong to the node.
    /// </summary>
    private void ContinueLastNode()
    {
        List<StatementSyntax> statements = ParseStatements(nodeLevel: true);
        if (statements.Count == 0)
            return;

        var node = (NodeSyntax)members[^1];
        members[^1] = node with { Body = [.. node.Body, .. statements], Span = new(node.Span.Start, statements[^1].Span.End - node.Span.Start) };
    }

    private void ParseTopLevelLine(SourceLine line)
    {
        bool first = !sawContent;
        sawContent = true;

        if (line.Kind is LineKind.At)
        {
            StartLine(line.Content);
            string word = CodeOf(token.Span);
            if (word is "@prefix")
            {
                ParsePrefix(line, first);
                return;
            }

            if (DeclarationKeywords.Contains(word))
            {
                members.Add(ParseDeclaration(line));
                return;
            }

            if (ExtensionKeywords.Contains(word))
            {
                FailUnexpected(token.Span);
                index++;
                SkipBlock();
                return;
            }
        }

        Report(DiagnosticCatalog.OutsideNode, new(line.Content.Start, PeekContentEnd(line) - line.Content.Start));
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
        scratch.Start(value);
        Token name = scratch.Next();
        if (tag.Value is not null && name.Kind is TokenKind.Name && name.Span == value)
            return new(tag.Value) { Span = value };

        Fail(DiagnosticCatalog.Missing, tag.Value is null ? new(tag.Span.End, 0) : value, "a node name", "#was:");
        return null;
    }

    private List<StatementSyntax> ParseStatements(bool nodeLevel)
    {
        if (!nodeLevel && !TryEnterBlock())
            return [];

        List<StatementSyntax> statements = [];
        while (!AtEndOfBlock)
        {
            if (Current.Kind is LineTokenKind.Indent)
            {
                ReportUnexpectedIndentation();
                statements.AddRange(ParseStatements(nodeLevel: false));
                SkipDedent();
            }
            else if (nodeLevel && (Current.Line.Kind is LineKind.Header || AtVariablesBeforeHeader()))
            {
                break;
            }
            else if (Current.Line.Kind is LineKind.Header)
            {
                Report(DiagnosticCatalog.Unexpected, new(Current.Line.Content.Start, 2), "a node header", null);
                index++;
                SkipBlock();
            }
            else
            {
                ParseStatement(statements, Current.Line);
            }
        }

        if (!nodeLevel)
            nesting--;

        return statements;
    }

    /// <summary>
    /// Enters one more level of block, just inside its indent. At the limit it reports the block's first line, skips the
    /// block and returns <see langword="false"/>, leaving the dedent that ends it for the caller.
    /// </summary>
    private bool TryEnterBlock()
    {
        if (nesting < MaxNesting)
        {
            nesting++;
            return true;
        }

        SourceLine line = Current.Line;
        Report(DiagnosticCatalog.NestingLimit, new(line.Content.Start, PeekContentEnd(line) - line.Content.Start), MaxNesting);
        SkipRestOfBlock();
        return false;
    }

    /// <summary>Enters one more level inside a line. At the limit it reports <paramref name="opener"/> and returns <see langword="false"/>.</summary>
    private bool TryEnter(TextSpan opener)
    {
        if (nesting >= MaxNesting)
        {
            Fail(DiagnosticCatalog.NestingLimit, opener, MaxNesting);
            return false;
        }

        nesting++;
        return true;
    }

    /// <summary>Skips to the dedent that ends the block being read, or to the end of the file, without reading it.</summary>
    private void SkipRestOfBlock()
    {
        for (int depth = 0; depth > 0 || !AtEndOfBlock; index++)
        {
            depth += Current.Kind switch
            {
                LineTokenKind.Indent => 1,
                LineTokenKind.Dedent => -1,
                _ => 0,
            };
        }
    }

    /// <summary>
    /// Whether the current line starts a run of variable declarations that reaches the next header or the end of the
    /// file, with no other line in between. Such a run belongs to the file, not to the node above it.
    /// </summary>
    private bool AtVariablesBeforeHeader()
    {
        for (int i = index; lines[i] is { Kind: LineTokenKind.Line, Line: var line }; i++)
        {
            if (line.Kind is LineKind.Header or LineKind.EndOfFile)
                return true;

            if (!IsVariableDeclaration(line))
                return false;
        }

        return false;
    }

    private bool IsVariableDeclaration(SourceLine line)
    {
        int start = line.Content.Start;
        return line.Kind is LineKind.At && code.AsSpan(start, Identifiers.Skip(code, start + 1, line.Content.End) - start) is "@var";
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
        lexer.Start(content, comments);
        lineFailed = false;
        previousEnd = content.Start;
        previousSpan = new(content.Start, 0);
        token = NextToken();
    }

    private void Advance()
    {
        previousSpan = token.Span;
        previousEnd = token.Span.End;
        token = NextToken();
    }

    /// <summary>Reads the next token. Once the lexer reports a problem on a line, the parser reports nothing more on it.</summary>
    private Token NextToken()
    {
        int count = diagnostics.Count;
        Token next = lexer.Next();
        if (diagnostics.Count > count)
            MuteLineOf(diagnostics[^1].Location.Span.Start);

        return next;
    }

    /// <summary>Makes the parser report nothing more on the line that holds <paramref name="position"/>.</summary>
    private void MuteLineOf(int position) => reportedLine = source.GetLineSpan(source.GetLinePosition(position).Line);

    private bool AtLineEnd => token.Kind is TokenKind.EndOfLine or TokenKind.Comment;

    /// <summary>Reports anything left on the line, unless the line already failed, and moves to the next line.</summary>
    private void FinishLine()
    {
        if (!AtLineEnd && !lineFailed)
            FailUnexpected(token.Span);

        index++;
    }

    private NameSyntax ExpectName(string what, string after)
    {
        if (token.Kind is TokenKind.Name)
        {
            var name = new NameSyntax(CodeOf(token.Span)) { Span = token.Span };
            Advance();
            return name;
        }

        Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), what, after);
        return new("") { Span = new(previousEnd, 0) };
    }

    /// <summary>Reads the <c>$variable</c> that follows <paramref name="after"/> on <paramref name="line"/>. A name without its <c>$</c> is reported and read as the variable.</summary>
    private VariableExpressionSyntax ExpectVariable(string after, SourceLine line)
    {
        if (token.Kind is TokenKind.Variable)
        {
            var variable = new VariableExpressionSyntax(CodeOf(token.Span)[1..]) { Span = token.Span };
            Advance();
            return variable;
        }

        if (token.Kind is TokenKind.Name)
        {
            TextSpan name = token.Span;
            string corrected = $"{source.Text[line.Content.Start..name.Start]}${source.Text[name.Start..PeekContentEnd(line)]}";
            Fail(DiagnosticCatalog.MissingDollar, name, TextOf(name), corrected);
            Advance();
            return new(CodeOf(name)) { Span = name };
        }

        Fail(DiagnosticCatalog.Missing, new(previousEnd, 0), "a variable", after);
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
        string text = source.Text;
        int name = tag.Span.Start + 1;
        int end = tag.Span.End;
        int colon = code.IndexOf(':', name, end - name);
        return colon < 0
            ? new TagSyntax(text[name..end], null) { Span = tag.Span }
            : new TagSyntax(text[name..colon], text[(colon + 1)..end]) { Span = tag.Span };
    }

    private TextSpan SpanFrom(int start) => new(start, previousEnd - start);

    private string TextOf(TextSpan span) => source.Text.Substring(span.Start, span.Length);

    /// <summary>The text at <paramref name="span"/> as the code-mode lexer reads it, with any marker typed through an input method read as the marker, for a token's name or keyword.</summary>
    private string CodeOf(TextSpan span) => code.Substring(span.Start, span.Length);

    /// <inheritdoc cref="CodeOf"/>
    private ReadOnlySpan<char> CodeSpanOf(TextSpan span) => code.AsSpan(span.Start, span.Length);

    /// <summary>Reports the line's first problem. Later problems on the same line are dropped.</summary>
    private void Fail(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments)
    {
        if (lineFailed)
            return;

        lineFailed = true;
        Report(descriptor, span, arguments);
    }

    /// <summary>Reports the text at <paramref name="span"/> as something the grammar doesn't allow where it appears.</summary>
    /// <param name="span">The unexpected text.</param>
    /// <param name="help">How to fix it, or <see langword="null"/> for none.</param>
    private void FailUnexpected(TextSpan span, string? help = null) => Fail(DiagnosticCatalog.Unexpected, span, $"`{TextOf(span)}`", help);

    /// <summary>Reports a problem with a whole line before the line is parsed, so parsing it reports nothing more.</summary>
    private void ReportLine(SourceLine line, DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments)
    {
        Report(descriptor, span, arguments);
        reportedLine = line.Content;
    }

    private void Report(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments)
    {
        if (reportedLine is { } line && span.Start >= line.Start && span.Start <= line.End)
            return;

        diagnostics.Add(descriptor.Create(source.GetLocation(span), arguments));
    }
}
