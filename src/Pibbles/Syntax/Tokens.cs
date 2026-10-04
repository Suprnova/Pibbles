namespace Pibbles.Syntax;

/// <summary>The kinds of token the code-mode lexer produces.</summary>
/// <remarks>
/// Keywords aren't kinds of their own. A keyword is a <see cref="Name"/> or <see cref="AtWord"/> spelled that way,
/// and the parser reads it as one only where the grammar expects it.
/// </remarks>
internal enum TokenKind
{
    Name,
    Variable,
    Number,
    Duration,
    String,
    Tag,
    Comment,
    AtWord,

    /// <summary>A <c>(</c> directly after a <see cref="Name"/>, which starts a call.</summary>
    CallOpen,

    OpenParen,
    CloseParen,
    CloseBracket,
    CloseBrace,
    Comma,
    Colon,
    Question,
    Equals,
    PlusEquals,
    MinusEquals,
    Arrow,
    EqualsEquals,
    BangEquals,
    Less,
    LessEquals,
    Greater,
    GreaterEquals,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    /// <summary>A character that starts no token. The parser reports it where it finds it.</summary>
    Bad,

    EndOfLine,
}

/// <summary>A token and the text it covers.</summary>
internal readonly record struct Token(TokenKind Kind, TextSpan Span);
