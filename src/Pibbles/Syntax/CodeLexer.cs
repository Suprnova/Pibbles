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
/// <see cref="TokenKind.CallOpen"/>. Malformed strings, numbers and tags are reported and still produce one token each.
/// </remarks>
internal sealed class CodeLexer(SourceText source, TextSpan span, List<Diagnostic> diagnostics)
{
    private readonly string text = source.Text;
    private TokenKind? previous;

    /// <summary>Where the next token starts looking.</summary>
    public int Position { get; private set; } = span.Start;

    /// <summary>Reads the next token, or <see cref="TokenKind.EndOfLine"/> at the end of the span.</summary>
    public Token Next()
    {
        int whitespace = Position;
        while (Position < span.End && text[Position] is ' ' or '\t')
            Position++;

        bool touching = Position == whitespace;
        int start = Position;
        TokenKind kind = Position < span.End ? Read(touching) : TokenKind.EndOfLine;
        previous = kind;
        return new(kind, new(start, Position - start));
    }

    private TokenKind Read(bool touching)
    {
        char c = text[Position];
        return c switch
        {
            '"' => ReadString(),
            '#' when IsIdentifierStart(Position + 1, allowUnderscore: false) => ReadTag(),
            '$' when IsIdentifierStart(Position + 1) => ReadPrefixed(TokenKind.Variable),
            '@' when IsIdentifierStart(Position + 1) => ReadPrefixed(TokenKind.AtWord),
            '/' when Peek(1) is '/' => ReadComment(),
            '.' when IsDigit(Peek(1)) => ReadNumber(),
            '.' when IsIdentifierStart(Position + 1) => ReadName(),
            '(' when touching && previous is TokenKind.Name => Punctuation(1, TokenKind.CallOpen),
            _ when IsDigit(c) => ReadNumber(),
            _ when IsIdentifierStart(Position) => ReadName(),
            _ => ReadPunctuation(c),
        };
    }

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
            if (text[Position] is '.')
                Position++;

            SkipIdentifier();
        }
        while (Peek(0) is '.' && IsIdentifierStart(Position + 1));

        return TokenKind.Name;
    }

    private TokenKind ReadPrefixed(TokenKind kind)
    {
        Position++;
        SkipIdentifier();
        return kind;
    }

    private TokenKind ReadComment()
    {
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

    /// <summary>Reads a quoted string. A backslash makes the punctuation after it literal.</summary>
    private TokenKind ReadString()
    {
        int start = Position++;
        while (Position < span.End && text[Position] is not '"')
        {
            if (text[Position] is '\\' && Position + 1 < span.End)
                ReadEscape();
            else
                Position++;
        }

        if (Position < span.End)
            Position++;
        else
            Report(DiagnosticCatalog.UnterminatedString, start);

        return TokenKind.String;
    }

    private void ReadEscape()
    {
        int start = Position++;
        Position += char.IsSurrogatePair(text, Position) ? 2 : 1;
        if (!IsEscapable(text[start + 1]))
            Report(DiagnosticCatalog.InvalidEscape, start, text[start..Position]);
    }

    /// <summary>Reads a tag. A name holding anything but letters, digits and <c>_</c> is reported, and runs to the next whitespace or <c>:</c>.</summary>
    private TokenKind ReadTag()
    {
        int start = Position++;
        SkipIdentifier();

        if (Position < span.End && text[Position] is not (' ' or '\t' or ':'))
        {
            while (Position < span.End && text[Position] is not (' ' or '\t' or ':'))
                Position++;

            string name = text[start..Position];
            Report(DiagnosticCatalog.MalformedTagName, start, name, Suggest(name));
        }

        if (Peek(0) is ':')
        {
            while (Position < span.End && text[Position] is not (' ' or '\t'))
                Position++;
        }

        return TokenKind.Tag;
    }

    private static string Suggest(string tag)
    {
        var suggestion = new StringBuilder("#");
        foreach (char c in tag.AsSpan(1))
            suggestion.Append(char.IsLetterOrDigit(c) || c is '_' ? c : '_');

        return suggestion.ToString();
    }

    private void SkipIdentifier()
    {
        while (Position < span.End && IsIdentifierPart(Position))
            Position += char.IsSurrogatePair(text, Position) ? 2 : 1;
    }

    private void SkipDigits()
    {
        while (IsDigit(Peek(0)))
            Position++;
    }

    private char Peek(int offset) => Position + offset < span.End ? text[Position + offset] : '\0';

    private bool IsIdentifierStart(int index, bool allowUnderscore = true) =>
        index < span.End && (char.IsLetter(text, index) || allowUnderscore && text[index] is '_');

    private bool IsIdentifierPart(int index) =>
        char.IsLetter(text, index)
        || IsDigit(text[index])
        || text[index] is '_'
        || CharUnicodeInfo.GetUnicodeCategory(text, index) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    private bool IsNumberContinuation(int index) =>
        index < span.End && (char.IsLetterOrDigit(text, index) || text[index] is '_' or '.');

    private static bool IsDigit(char c) => char.IsAsciiDigit(c);

    /// <summary>ASCII punctuation, the characters a backslash can make literal.</summary>
    private static bool IsEscapable(char c) => char.IsAscii(c) && (char.IsPunctuation(c) || char.IsSymbol(c));

    private void Report(DiagnosticDescriptor descriptor, int start, params object?[] arguments) =>
        diagnostics.Add(descriptor.Create(source.GetLocation(new(start, Position - start)), arguments));
}
