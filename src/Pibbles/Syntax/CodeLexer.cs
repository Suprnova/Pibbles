using System.Globalization;
using System.Text;
using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// Reads code-mode tokens from part of a line: header and <c>@</c> lines after their marker, and everything inside
/// <c>[…]</c> and <c>{…}</c>, following the tokens layer of the grammar in <c>docs/language/reference.md</c>.
/// </summary>
/// <remarks>
/// Whitespace between tokens is skipped, except that a <c>(</c> touching the name before it is a
/// <see cref="TokenKind.CallOpen"/>, unless the name is <c>and</c>, <c>or</c> or <c>not</c>. Malformed strings, numbers and tags are reported and still produce one token each.
/// A form an input method types in a marker's place, such as <c>＝</c> or <c>」</c>, is read as its marker and recorded,
/// except inside a string, a tag's value or a comment, which keep what's written. A parser keeps one lexer and calls
/// <see cref="Start"/> for each stretch of code it reads.
/// </remarks>
/// <param name="source">The file the stretches are in.</param>
/// <param name="diagnostics">Where problems are reported, or <see langword="null"/> to read without reporting them.</param>
/// <param name="forms">Where forms read as markers are recorded, or <see langword="null"/> to read them without recording them.</param>
/// <param name="code">The file's text with its forms mapped, from <see cref="MarkerForms.Map(string)"/>, if the caller already has it.</param>
internal sealed class CodeLexer(SourceText source, List<Diagnostic>? diagnostics, MarkerFormLog? forms = null, string? code = null)
{
    private readonly string text = source.Text;
    private readonly string code = code ?? MarkerForms.Map(source.Text);
    private TextSpan span;
    private List<Comment>? comments;
    private bool tagMayBeText;
    private TokenKind? previous;
    private int previousStart;

    /// <summary>Where the next token starts looking.</summary>
    public int Position { get; private set; }

    /// <summary>Starts reading <paramref name="span"/>, as a new lexer would.</summary>
    /// <param name="span">The stretch to read.</param>
    /// <param name="comments">Where a comment that ends the stretch is recorded, or <see langword="null"/> to skip it.</param>
    /// <param name="tagMayBeText">Whether a malformed tag in the stretch may be text meant to show, so its report offers the escape.</param>
    public void Start(TextSpan span, List<Comment>? comments = null, bool tagMayBeText = false)
    {
        this.span = span;
        this.comments = comments;
        this.tagMayBeText = tagMayBeText;
        previous = null;
        Position = span.Start;
    }

    /// <summary>Reads the next token, or <see cref="TokenKind.EndOfLine"/> at the end of the span.</summary>
    public Token Next()
    {
        int whitespace = Position;
        Position = Whitespace.Skip(text, Position, span.End);

        bool touching = Position == whitespace;
        int start = Position;
        TokenKind kind = Position < span.End ? Read(touching) : TokenKind.EndOfLine;
        previous = kind;
        previousStart = start;
        if (kind is not (TokenKind.String or TokenKind.Tag or TokenKind.Comment or TokenKind.Bad or TokenKind.EndOfLine))
            RecordForms(start, Position);

        return new(kind, new(start, Position - start));
    }

    private TokenKind Read(bool touching)
    {
        char c = code[Position];
        return c switch
        {
            '"' => ReadString(),
            '#' when AtIdentifierStart(Position + 1, allowUnderscore: false) => ReadTag(),
            '$' when IsNameStart(Position + 1) => ReadVariable(),
            '@' when AtIdentifierStart(Position + 1) => ReadAtWord(),
            '/' when Peek(1) is '/' => ReadComment(),
            '.' when IsDigit(Peek(1)) => ReadNumber(),
            '.' when AtIdentifierStart(Position + 1) => ReadName(),
            '(' when touching && previous is TokenKind.Name && !AfterOperatorWord() => Punctuation(1, TokenKind.CallOpen),
            _ when IsDigit(c) => ReadNumber(),
            _ when AtIdentifierStart(Position) => ReadName(),
            _ => ReadPunctuation(c),
        };
    }

    private bool AfterOperatorWord() => code.AsSpan(previousStart, Position - previousStart) is "and" or "or" or "not";

    private TokenKind ReadPunctuation(char c) => (c, Peek(1)) switch
    {
        ('-', '>') => Punctuation(2, TokenKind.Arrow),
        ('+', '=') => Punctuation(2, TokenKind.PlusEquals),
        ('-', '=') => Punctuation(2, TokenKind.MinusEquals),
        ('=', '=') => Punctuation(2, TokenKind.EqualsEquals),
        ('!', '=') => Punctuation(2, TokenKind.BangEquals),
        ('<', '=') => Punctuation(2, TokenKind.LessEquals),
        ('>', '=') => Punctuation(2, TokenKind.GreaterEquals),
        ('(', _) => Punctuation(1, TokenKind.OpenParen),
        (')', _) => Punctuation(1, TokenKind.CloseParen),
        (']', _) => Punctuation(1, TokenKind.CloseBracket),
        ('}', _) => Punctuation(1, TokenKind.CloseBrace),
        (',', _) => Punctuation(1, TokenKind.Comma),
        (':', _) => Punctuation(1, TokenKind.Colon),
        ('?', _) => Punctuation(1, TokenKind.Question),
        ('=', _) => Punctuation(1, TokenKind.Equals),
        ('<', _) => Punctuation(1, TokenKind.Less),
        ('>', _) => Punctuation(1, TokenKind.Greater),
        ('+', _) => Punctuation(1, TokenKind.Plus),
        ('-', _) => Punctuation(1, TokenKind.Minus),
        ('*', _) => Punctuation(1, TokenKind.Star),
        ('/', _) => Punctuation(1, TokenKind.Slash),
        ('%', _) => Punctuation(1, TokenKind.Percent),
        _ => Punctuation(char.IsSurrogatePair(text, Position) ? 2 : 1, TokenKind.Bad),
    };

    private TokenKind Punctuation(int length, TokenKind kind)
    {
        Position += length;
        return kind;
    }

    private TokenKind ReadName()
    {
        do
        {
            if (code[Position] is '.')
                Position++;

            SkipIdentifier();
        }
        while (Peek(0) is '.' && AtIdentifierStart(Position + 1));

        return TokenKind.Name;
    }

    private TokenKind ReadVariable()
    {
        Position++;
        ReadName();
        return TokenKind.Variable;
    }

    private TokenKind ReadAtWord()
    {
        Position++;
        SkipIdentifier();
        return TokenKind.AtWord;
    }

    private TokenKind ReadComment()
    {
        RecordForms(Position, Position + 2);
        if (Comment.IsNote(code.AsSpan(Position, span.End - Position)))
            diagnostics?.Report(source, DiagnosticCatalog.UnsupportedNote, new(Position, 3));
        else
            comments?.Add(Comment.Read(text, new(Position, span.End - Position), onOwnLine: false));

        Position = span.End;
        return TokenKind.Comment;
    }

    /// <summary>Reads <c>digit+ ("." digit*)?</c> or <c>"." digit+</c>, then an optional <c>ms</c> or <c>s</c>.</summary>
    private TokenKind ReadNumber()
    {
        int start = Position;
        SkipDigits();
        if (Peek(0) is '.')
        {
            Position++;
            SkipDigits();
        }

        TokenKind kind = TokenKind.Number;
        int unit = Peek(0) is 'm' && Peek(1) is 's' ? 2 : Peek(0) is 's' ? 1 : 0;
        if (unit > 0 && !IsNumberContinuation(Position + unit))
        {
            Position += unit;
            kind = TokenKind.Duration;
        }

        if (!IsNumberContinuation(Position))
            return kind;

        while (IsNumberContinuation(Position))
            Position += char.IsSurrogatePair(text, Position) ? 2 : 1;

        Report(DiagnosticCatalog.MalformedNumber, start, text[start..Position]);
        return TokenKind.Number;
    }

    /// <summary>
    /// Reads a quoted string. A backslash makes the punctuation after it literal. A string opened with <c>"</c> closes only at
    /// <c>"</c>, so it can hold <c>“”</c>. One opened with a form such as <c>“</c> closes at <c>"</c> or any of its forms.
    /// </summary>
    private TokenKind ReadString()
    {
        int start = Position++;
        RecordForms(start, start + 1);
        string closers = text[start] is '"' ? text : code;
        while (Position < span.End && closers[Position] is not '"')
        {
            if (text[Position] is '\\' && Position + 1 < span.End)
                ReadEscape();
            else
                Position++;
        }

        if (Position < span.End)
            RecordForms(Position, ++Position);
        else
            Report(DiagnosticCatalog.UnterminatedString, start);

        return TokenKind.String;
    }

    private void ReadEscape()
    {
        int start = Position++;
        Position += char.IsSurrogatePair(text, Position) ? 2 : 1;
        if (!IsEscapable(text, start + 1))
            Report(DiagnosticCatalog.InvalidEscape, start, text[start..Position]);
    }

    /// <summary>Reads a tag. A name holding anything but letters, digits and <c>_</c> is reported, and runs to the next whitespace, <c>:</c>, <c>[</c>, <c>{</c> or comment.</summary>
    private TokenKind ReadTag()
    {
        int start = Position++;
        RecordForms(start, Position);
        SkipIdentifier();

        if (!AtTagNameEnd())
        {
            while (!AtTagNameEnd())
                Position++;

            string name = text[start..Position];
            string help = $"Write `{Suggest(name)}`.";
            Report(DiagnosticCatalog.MalformedTagName, start, name, tagMayBeText ? $"{help}\n{DiagnosticCatalog.EscapeHelp(name)}" : help);
        }

        if (Peek(0) is ':')
        {
            RecordForms(Position, Position + 1);
            while (Position < span.End && !Whitespace.Is(text[Position]))
                Position++;
        }

        return TokenKind.Tag;
    }

    private bool AtTagNameEnd() => Position >= span.End || Whitespace.Is(text[Position]) || code[Position] is ':' or '[' or '{' || (Peek(0) is '/' && Peek(1) is '/');

    private static string Suggest(string tag)
    {
        var suggestion = new StringBuilder("#");
        for (int index = 1; index < tag.Length;)
        {
            int length = char.IsSurrogatePair(tag, index) ? 2 : 1;
            suggestion.Append(Identifiers.IsPart(tag, index) ? tag.AsSpan(index, length) : "_");
            index += length;
        }

        return suggestion.ToString();
    }

    private void SkipIdentifier() => Position = Identifiers.Skip(text, Position, span.End);

    private void SkipDigits()
    {
        while (IsDigit(Peek(0)))
            Position++;
    }

    private char Peek(int offset) => Position + offset < span.End ? code[Position + offset] : '\0';

    private bool AtIdentifierStart(int index, bool allowUnderscore = true) =>
        index < span.End && Identifiers.IsStart(text, index) && (allowUnderscore || text[index] is not '_');

    private bool IsNameStart(int index) => AtIdentifierStart(index) || index < span.End && code[index] is '.' && AtIdentifierStart(index + 1);

    private bool IsNumberContinuation(int index) =>
        index < span.End && (Identifiers.IsPart(text, index) || code[index] is '.');

    private static bool IsDigit(char c) => char.IsAsciiDigit(c);

    /// <summary>Whether the code point at <paramref name="index"/> is punctuation or a symbol, in any script: what a backslash can make literal. U+FFFC, which stands for an icon, is never literal.</summary>
    internal static bool IsEscapable(string text, int index) =>
        text[index] is not '\uFFFC'
        && CharUnicodeInfo.GetUnicodeCategory(text, index) is
            UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;

    /// <summary>Records each form from <paramref name="start"/> to <paramref name="end"/> as the marker it's read as.</summary>
    private void RecordForms(int start, int end)
    {
        if (forms is null || ReferenceEquals(code, text))
            return;

        for (int i = start; i < end; i++)
        {
            if (code[i] != text[i])
                forms.Record(i, code[i]);
        }
    }

    private void Report(DiagnosticDescriptor descriptor, int start, params object?[] arguments) =>
        diagnostics?.Report(source, descriptor, new(start, Position - start), arguments);
}
