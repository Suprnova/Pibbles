using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class CodeLexerTests
{
    private readonly List<Diagnostic> diagnostics = [];

    [Theory]
    [InlineData("kitchen.door", "Name kitchen.door")]
    [InlineData(".door", "Name .door")]
    [InlineData("rooms.kitchen.door", "Name rooms.kitchen.door")]
    [InlineData("_hidden a1_b", "Name _hidden|Name a1_b")]
    [InlineData("café", "Name café")]
    [InlineData("kitchen.", "Name kitchen|Bad .")]
    [InlineData("$has_key", "Variable $has_key")]
    [InlineData("@shake_screen", "AtWord @shake_screen")]
    [InlineData("3 0.5 .5 1.", "Number 3|Number 0.5|Number .5|Number 1.")]
    [InlineData("0.5s 300ms .5s 1.s", "Duration 0.5s|Duration 300ms|Duration .5s|Duration 1.s")]
    [InlineData("\"Hello there.\"", "String \"Hello there.\"")]
    [InlineData("#thought #box:phone #box: #id:k7qp2x", "Tag #thought|Tag #box:phone|Tag #box:|Tag #id:k7qp2x")]
    [InlineData("#box:a-b.c/d", "Tag #box:a-b.c/d")]
    [InlineData("jump // a comment", "Name jump|Comment // a comment")]
    [InlineData("$a-1", "Variable $a|Minus -|Number 1")]
    public void Next_SingleConstruct_ReadsOneTokenEach(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("( ) ] } , : ? = += -= ->", "OpenParen (|CloseParen )|CloseBracket ]|CloseBrace }|Comma ,|Colon :|Question ?|Equals =|PlusEquals +=|MinusEquals -=|Arrow ->")]
    [InlineData("== != < <= > >= + - * / %", "EqualsEquals ==|BangEquals !=|Less <|LessEquals <=|Greater >|GreaterEquals >=|Plus +|Minus -|Star *|Slash /|Percent %")]
    [InlineData("a==b", "Name a|EqualsEquals ==|Name b")]
    public void Next_Punctuation_TakesLongestMatch(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("has_item(\"key\")", "Name has_item|CallOpen (|String \"key\"|CloseParen )")]
    [InlineData("has_item (\"key\")", "Name has_item|OpenParen (|String \"key\"|CloseParen )")]
    [InlineData("kitchen.door(x)", "Name kitchen.door|CallOpen (|Name x|CloseParen )")]
    [InlineData("@show(x)", "AtWord @show|OpenParen (|Name x|CloseParen )")]
    [InlineData("$x(y)", "Variable $x|OpenParen (|Name y|CloseParen )")]
    [InlineData("(x)(y)", "OpenParen (|Name x|CloseParen )|OpenParen (|Name y|CloseParen )")]
    public void Next_OpenParen_IsCallOnlyWhenTouchingName(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("$", "Bad $")]
    [InlineData("@ home", "Bad @|Name home")]
    [InlineData("#1", "Bad #|Number 1")]
    [InlineData("#_x", "Bad #|Name _x")]
    [InlineData("!", "Bad !")]
    [InlineData("&", "Bad &")]
    public void Next_CharacterStartingNoToken_IsBad(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Fact]
    public void Next_NameWithCombiningMark_IsOneName()
    {
        string name = $"nai{(char)0x0308}ve";

        string tokens = Describe(name);

        Assert.Equal($"Name {name}", tokens);
    }

    [Fact]
    public void Next_SupplementaryLetter_IsPartOfName()
    {
        string name = char.ConvertFromUtf32(0x1D4B3) + "x";

        string tokens = Describe(name);

        Assert.Equal($"Name {name}", tokens);
    }

    [Fact]
    public void Next_EmptySpan_IsEndOfLine()
    {
        var lexer = new CodeLexer(new SourceText("story.pib", "   "), new(0, 3), diagnostics);

        Token token = lexer.Next();

        Assert.Equal(new(TokenKind.EndOfLine, new(3, 0)), token);
    }

    [Fact]
    public void Next_SpanInsideLine_StopsAtSpanEnd()
    {
        var lexer = new CodeLexer(new SourceText("story.pib", "{$a} text"), new(1, 2), diagnostics);

        (Token first, Token second) = (lexer.Next(), lexer.Next());

        Assert.Equal((new Token(TokenKind.Variable, new(1, 2)), new Token(TokenKind.EndOfLine, new(3, 0))), (first, second));
    }

    [Fact]
    public void Next_UnterminatedString_RunsToEndAndReports()
    {
        string tokens = Describe("\"Never ends");

        Assert.Equal("String \"Never ends", tokens);
        Assert.Equal(("PIB1041", new TextSpan(0, 11)), (diagnostics[0].Code, diagnostics[0].Location.Span));
    }

    [Theory]
    [InlineData("1e5", "1e5")]
    [InlineData("1_000 x", "1_000")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("2sec", "2sec")]
    public void Next_MalformedNumber_IsOneNumberAndReports(string text, string number)
    {
        Token token = new CodeLexer(new SourceText("story.pib", text), new(0, text.Length), diagnostics).Next();

        Assert.Equal((TokenKind.Number, number.Length), (token.Kind, token.Span.Length));
        Assert.Equal(("PIB1042", $"`{number}` isn't a number I can read."), (diagnostics[0].Code, diagnostics[0].Message));
    }

    [Theory]
    [InlineData("#show-disabled", "#show_disabled")]
    [InlineData("#box.big:phone", "#box_big")]
    public void Next_MalformedTagName_ReportsSuggestion(string text, string suggestion)
    {
        string tokens = Describe(text);

        Assert.Equal($"Tag {text}", tokens);
        Assert.Equal(("PIB1043", $"Write `{suggestion}`."), (diagnostics[0].Code, diagnostics[0].Help));
    }

    [Theory]
    [InlineData("\"\\n\"", "\\n")]
    [InlineData("\"\\a\"", "\\a")]
    [InlineData("\"\\ \"", "\\ ")]
    public void Next_EscapeBeforeNonPunctuation_Reports(string text, string escape)
    {
        Describe(text);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(("PIB1014", $"I don't know the escape `{escape}`."), (diagnostic.Code, diagnostic.Message));
    }

    [Fact]
    public void Next_EscapedPunctuation_ReportsNothing()
    {
        Describe("\"\\\" \\\\ \\# \\{ \\$ \\~ \\_\"");

        Assert.Empty(diagnostics);
    }

    private string Describe(string text)
    {
        var source = new SourceText("story.pib", text);
        var lexer = new CodeLexer(source, new(0, text.Length), diagnostics);
        List<string> tokens = [];

        for (Token token = lexer.Next(); token.Kind is not TokenKind.EndOfLine; token = lexer.Next())
            tokens.Add($"{token.Kind} {text.Substring(token.Span.Start, token.Span.Length)}");

        return string.Join('|', tokens);
    }
}
