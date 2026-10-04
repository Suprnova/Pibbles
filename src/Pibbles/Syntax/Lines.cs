namespace Pibbles.Syntax;

/// <summary>What a line is, decided by how its content starts.</summary>
internal enum LineKind
{
    Blank,
    Note,
    Comment,
    Header,
    At,
    Option,
    Dash,
    Text,

    /// <summary>The end of the file. The classifier ends every token stream with one.</summary>
    EndOfFile,
}

/// <summary>One classified line.</summary>
/// <param name="Number">The 0-based line number.</param>
/// <param name="Kind">What the line is.</param>
/// <param name="Content">The line after its indentation, without its line break.</param>
internal readonly record struct Line(int Number, LineKind Kind, TextSpan Content);

internal enum LineTokenKind
{
    Indent,
    Dedent,
    Line,
}

/// <summary>A token of the lines layer: a line, or an indent or dedent before one.</summary>
/// <param name="Kind">What the token is.</param>
/// <param name="Line">The line itself, or the line an indent or dedent comes before.</param>
internal readonly record struct LineToken(LineTokenKind Kind, Line Line);
