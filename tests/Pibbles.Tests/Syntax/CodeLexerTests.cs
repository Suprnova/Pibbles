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
    [InlineData("$kitchen.door_open", "Variable $kitchen.door_open")]
    [InlineData("$rooms.kitchen.door_open", "Variable $rooms.kitchen.door_open")]
    [InlineData("$.door_open", "Variable $.door_open")]
    [InlineData("$a .b", "Variable $a|Name .b")]
    [InlineData("$a.", "Variable $a|Bad .")]
    [InlineData("@shake_screen", "AtWord @shake_screen")]
    [InlineData("3 0.5 .5 1.", "Number 3|Number 0.5|Number .5|Number 1.")]
    [InlineData("0.5s 300ms .5s 1.s", "Duration 0.5s|Duration 300ms|Duration .5s|Duration 1.s")]
    [InlineData("\"Hello there.\"", "String \"Hello there.\"")]
    [InlineData("#thought #box:phone #box: #id:k7qp2x", "Tag #thought|Tag #box:phone|Tag #box:|Tag #id:k7qp2x")]
    [InlineData("#box:a-b.c/d", "Tag #box:a-b.c/d")]
    [InlineData("jump // a comment", "Name jump|Comment // a comment")]
    [InlineData("#thought//a comment", "Tag #thought|Comment //a comment")]
    [InlineData("#thought:a//b #next", "Tag #thought:a//b|Tag #next")]
    [InlineData("#show-disabled//a comment", "Tag #show-disabled|Comment //a comment")]
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
    [InlineData("$kitchen.x(y)", "Variable $kitchen.x|OpenParen (|Name y|CloseParen )")]
    [InlineData("(x)(y)", "OpenParen (|Name x|CloseParen )|OpenParen (|Name y|CloseParen )")]
    [InlineData("nothing(x)", "Name nothing|CallOpen (|Name x|CloseParen )")]
    [InlineData("android(x)", "Name android|CallOpen (|Name x|CloseParen )")]
    [InlineData("kitchen.not(x)", "Name kitchen.not|CallOpen (|Name x|CloseParen )")]
    public void Next_OpenParen_IsCallOnlyWhenTouchingName(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("not($x)", "Name not|OpenParen (|Variable $x|CloseParen )")]
    [InlineData("$a and($b)", "Variable $a|Name and|OpenParen (|Variable $b|CloseParen )")]
    [InlineData("$a or($b)", "Variable $a|Name or|OpenParen (|Variable $b|CloseParen )")]
    [InlineData("not（$x）", "Name not|OpenParen （|Variable $x|CloseParen ）")]
    public void Next_OpenParenTouchingOperatorWord_IsNotCall(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData($"jump{Spaces.Ideographic}kitchen.door", "Name jump|Name kitchen.door")]
    [InlineData($"a{Spaces.NoBreak}={Spaces.NarrowNoBreak}b", "Name a|Equals =|Name b")]
    [InlineData($"has_item{Spaces.Ideographic}(x)", "Name has_item|OpenParen (|Name x|CloseParen )")]
    [InlineData($"#box:phone{Spaces.Ideographic}#thought", "Tag #box:phone|Tag #thought")]
    [InlineData($"#box{Spaces.NoBreak}#thought", "Tag #box|Tag #thought")]
    [InlineData($"#thought{Spaces.Ideographic}// a comment", "Tag #thought|Comment // a comment")]
    public void Next_OtherSpaceSeparator_SeparatesLikeASpace(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("$", "Bad $")]
    [InlineData("$.", "Bad $|Bad .")]
    [InlineData("$.1", "Bad $|Number .1")]
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
        CodeLexer lexer = Lexer("   ", new(0, 3));

        Token token = lexer.Next();

        Assert.Equal(new(TokenKind.EndOfLine, new(3, 0)), token);
    }

    [Fact]
    public void Next_SpanInsideLine_StopsAtSpanEnd()
    {
        CodeLexer lexer = Lexer("{$a} text", new(1, 2));

        (Token first, Token second) = (lexer.Next(), lexer.Next());

        Assert.Equal((new Token(TokenKind.Variable, new(1, 2)), new Token(TokenKind.EndOfLine, new(3, 0))), (first, second));
    }

    [Fact]
    public void Next_UnterminatedString_RunsToEndAndReports()
    {
        string tokens = Describe("\"Never ends");

        Assert.Equal("String \"Never ends", tokens);
        Assert.Equal(("PIB1104", new TextSpan(0, 11)), (diagnostics[0].Code, diagnostics[0].Location.Span));
    }

    [Theory]
    [InlineData("1e5", "1e5")]
    [InlineData("1_000 x", "1_000")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("2sec", "2sec")]
    public void Next_MalformedNumber_IsOneNumberAndReports(string text, string number)
    {
        Token token = Lexer(text, new(0, text.Length)).Next();

        Assert.Equal((TokenKind.Number, number.Length), (token.Kind, token.Span.Length));
        Assert.Equal(("PIB1105", $"`{number}` isn't a number I can read."), (diagnostics[0].Code, diagnostics[0].Message));
    }

    [Fact]
    public void Next_NumberBeforeCombiningMark_IsOneNumberAndOneDiagnostic()
    {
        string tokens = Describe("5é");

        Assert.Equal("Number 5é", tokens);
        Assert.Equal("PIB1105", Assert.Single(diagnostics).Code);
    }

    [Theory]
    [InlineData("#show-disabled", "#show_disabled")]
    [InlineData("#box.big:phone", "#box_big")]
    [InlineData("#tag٣", "#tag_")]
    [InlineData("#a.𝐀", "#a_𝐀")]
    public void Next_MalformedTagName_ReportsSuggestion(string text, string suggestion)
    {
        string tokens = Describe(text);

        Assert.Equal($"Tag {text}", tokens);
        Assert.Equal(("PIB1202", $"Write `{suggestion}`."), (diagnostics[0].Code, diagnostics[0].Help));
    }

    [Theory]
    [InlineData("#winning[/b]", "#winning")]
    [InlineData("#winning{/if}", "#winning")]
    public void Next_TagBeforeBracketOrBrace_EndsAtIt(string text, string tag)
    {
        Token token = Lexer(text, new(0, text.Length)).Next();

        Assert.Equal((TokenKind.Tag, tag.Length), (token.Kind, token.Span.Length));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Next_MalformedTagNameBeforeBracket_ReportsOnlyTheName()
    {
        Token token = Lexer("#win-ning[/b]", new(0, 13)).Next();

        Assert.Equal(("#win-ning".Length, "PIB1202", "Write `#win_ning`."), (token.Span.Length, diagnostics[0].Code, diagnostics[0].Help));
    }

    [Fact]
    public void Next_MalformedTagNameThatMayBeText_OffersTheEscape()
    {
        var lexer = new CodeLexer(new SourceText("story.pib", "#team!"), diagnostics);
        lexer.Start(new(0, 6), tagMayBeText: true);

        lexer.Next();

        Assert.Equal("Write `#team_`.\nIf `#team!` is text, put a backslash before the `#`.", Assert.Single(diagnostics).Help);
    }

    [Theory]
    [InlineData("\"\\n\"", "\\n")]
    [InlineData("\"\\a\"", "\\a")]
    [InlineData("\"\\ \"", "\\ ")]
    [InlineData("\"\\あ\"", "\\あ")]
    [InlineData($"\"\\{Spaces.Ideographic}\"", $"\\{Spaces.Ideographic}")]
    public void Next_EscapeBeforeNonPunctuation_Reports(string text, string escape)
    {
        Describe(text);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(("PIB1101", $"I don't know the escape `{escape}`."), (diagnostic.Code, diagnostic.Message));
    }

    [Fact]
    public void Next_EscapedPunctuation_ReportsNothing()
    {
        Describe("\"\\\" \\\\ \\# \\{ \\$ \\~ \\_\"");

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("：")]
    [InlineData("＠")]
    [InlineData("。")]
    [InlineData("￥")]
    [InlineData("「")]
    [InlineData("\U0001F600")]
    public void Next_EscapedNonAsciiPunctuationOrSymbol_ReportsNothing(string escaped)
    {
        Describe($"\"\\{escaped}\"");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Next_EscapedObjectReplacementCharacter_Reports()
    {
        Describe($"\"\\{(char)0xFFFC}\"");

        Assert.Equal("PIB1101", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Next_WithoutDiagnostics_ReadsMalformedTokenSilently()
    {
        var lexer = new CodeLexer(new SourceText("story.pib", "1e5"), diagnostics: null);
        lexer.Start(new(0, 3));

        Token token = lexer.Next();

        Assert.Equal(new(TokenKind.Number, new(0, 3)), token);
    }

    [Fact]
    public void Start_AfterName_ReadsTouchingParenAsOpenParen()
    {
        CodeLexer lexer = Lexer("a(", new(0, 1));
        lexer.Next();
        lexer.Start(new(1, 1));

        Token token = lexer.Next();

        Assert.Equal(new(TokenKind.OpenParen, new(1, 1)), token);
    }

    [Fact]
    public void Start_WithoutComments_RecordsOnlyEarlierStretchComments()
    {
        List<Comment> comments = [];
        var lexer = new CodeLexer(new SourceText("story.pib", "a // one\nb // two"), diagnostics);
        lexer.Start(new(0, 8), comments);
        ReadToEnd(lexer);
        lexer.Start(new(9, 8));

        ReadToEnd(lexer);

        Assert.Equal("one", Assert.Single(comments).Text);
    }

    private CodeLexer Lexer(string text, TextSpan span)
    {
        var lexer = new CodeLexer(new SourceText("story.pib", text), diagnostics);
        lexer.Start(span);
        return lexer;
    }

    [Theory]
    [InlineData("＄met ＝ true", "Variable ＄met|Equals ＝|Name true")]
    [InlineData("￥met", "Variable ￥met")]
    [InlineData("ime。case", "Name ime。case")]
    [InlineData("０。５s", "Duration ０。５s")]
    [InlineData("has_item（“key”）", "Name has_item|CallOpen （|String “key”|CloseParen ）")]
    [InlineData("＃box：phone", "Tag ＃box：phone")]
    [InlineData("x ／／ note", "Name x|Comment ／／ note")]
    [InlineData("a、b」", "Name a|Comma 、|Name b|CloseBracket 」")]
    [InlineData("＠show", "AtWord ＠show")]
    public void Next_MarkerTypedThroughInputMethod_ReadsAsTheMarker(string text, string expected)
    {
        string tokens = Describe(text);

        Assert.Equal(expected, tokens);
    }

    [Fact]
    public void Next_StringOpenedWithAsciiQuote_ClosesOnlyAtAsciiQuote() => Assert.Equal("String \"he said “hi”\"", Describe("\"he said “hi”\""));

    [Fact]
    public void Next_FormsInStringTagValueAndComment_RecordsOnlyTheMarkers()
    {
        var source = new SourceText("story.pib", "“「x」” ＃box：「y」 ／／ 「z」");
        var forms = new MarkerFormLog();
        var lexer = new CodeLexer(source, diagnostics, forms);
        lexer.Start(new(0, source.Text.Length));

        ReadToEnd(lexer);

        Assert.Equal([(0, 1), (4, 1), (6, 1), (10, 1), (15, 2)], forms.Report(source).Select(diagnostic => (diagnostic.Location.Span.Start, diagnostic.Location.Span.Length)));
    }

    private static void ReadToEnd(CodeLexer lexer)
    {
        while (lexer.Next().Kind is not TokenKind.EndOfLine)
        {
        }
    }

    private string Describe(string text)
    {
        CodeLexer lexer = Lexer(text, new(0, text.Length));
        List<string> tokens = [];

        for (Token token = lexer.Next(); token.Kind is not TokenKind.EndOfLine; token = lexer.Next())
            tokens.Add($"{token.Kind} {text.Substring(token.Span.Start, token.Span.Length)}");

        return string.Join('|', tokens);
    }
}
