using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

public class ParserTests
{
    [Theory]
    [InlineData("@pause 0.1", "0.1")]
    [InlineData("@pause 1.50", "1.50")]
    [InlineData("@pause 300ms", "0.3")]
    [InlineData("@pause 1.5s", "1.5")]
    [InlineData("@pause 79228162514264337593543950335", "79228162514264337593543950335")]
    [InlineData("@pause 1.12345678901234567890123456789", "1.1234567890123456789012345679")]
    public void Parse_NumberOrDuration_IsExactDecimalWithItsScale(string text, string expected)
    {
        var source = new SourceText("story.pib", $"== a.b\n{text}\n");

        var pause = (PauseStatementSyntax)SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        decimal value = pause.Duration switch
        {
            NumberLiteralSyntax number => number.Value,
            DurationLiteralSyntax duration => duration.Seconds,
            _ => throw new Xunit.Sdk.XunitException("Not a literal."),
        };
        Assert.Equal(expected, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_AtWait_IsAnOrdinaryCommand()
    {
        var source = new SourceText("story.pib", "== a.b\n@wait 1s\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal("wait", Assert.IsType<CommandStatementSyntax>(tree.Root.Nodes[0].Body[0]).Command.Text);
    }

    [Theory]
    [InlineData("@pause 79228162514264337593543950336")]
    [InlineData("@pause 100000000000000000000000000000s")]
    public void Parse_NumberTooLarge_ReportsItAndMakesAnErrorNode(string text)
    {
        var source = new SourceText("story.pib", $"== a.b\n{text}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1047", Assert.Single(tree.Diagnostics).Code);
        Assert.IsType<ErrorExpressionSyntax>(((PauseStatementSyntax)tree.Root.Nodes[0].Body[0]).Duration);
    }

    [Fact]
    public void Parse_AtWordWithNonAsciiDigitInAlternative_ReportsTheOpener()
    {
        var source = new SourceText("story.pib", "== fixture.start\n@cycle\n    - @if٣ $a\n");

        var tree = SyntaxTree.Parse(source);

        Diagnostic diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(("PIB1032", "`@if` can't start an alternative."), (diagnostic.Code, diagnostic.Message));
    }

    [Theory]
    [InlineData("@cycle\n    - @elif $a\n", "PIB1032")]
    [InlineData("@cycle\n    - @if $a\n", "PIB1032")]
    [InlineData("@cycle\n    - @if $a:\n", "PIB1032")]
    [InlineData("@cycle\n    @var $a = 1\n", "PIB1031")]
    public void Parse_LineReportedInVariationBlock_ReportsNothingMoreOnIt(string block, string code)
    {
        var source = new SourceText("story.pib", $"== fixture.start\n{block}");

        Assert.Equal(code, Assert.Single(SyntaxTree.Parse(source).Diagnostics).Code);
    }

    [Theory]
    [InlineData("@prefix kitchen\n@prefix cellar", 1)]
    [InlineData("// A comment.\n\n@prefix kitchen\n== .door\n@prefix cellar", 3)]
    public void Parse_SecondPrefix_NamesLineOfFirst(string text, int line)
    {
        var source = new SourceText("story.pib", text);

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1023", $"This file already has a `@prefix`, on line {line}."), (diagnostic.Code, diagnostic.Message));
    }

    [Theory]
    [InlineData("mira: I'm #winning today.", "what mira says", "mira: I'm \\#winning today.")]
    [InlineData("I'm #winning today.", "the text", "I'm \\#winning today.")]
    [InlineData("-> Go #show_disabled @once", "the option's text", "-> Go \\#show_disabled @once")]
    public void Parse_TextAfterTag_HelpNamesWhoseTextAndShowsFixedLine(string line, string owner, string fixedLine)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}  \n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        string tag = line[line.IndexOf('#', StringComparison.Ordinal)..].Split(' ')[0];
        Assert.Equal($"If `{tag}` is part of {owner}, put a backslash before the `#`:\n{fixedLine}", diagnostic.Help);
    }

    [Theory]
    [InlineData("mira: [b]I'm #winning[/b]", "#winning", "mira: [b]I'm \\#winning[/b]")]
    [InlineData("mira: {if $a}I'm #winning{/if}", "#winning", "mira: {if $a}I'm \\#winning{/if}")]
    [InlineData("mira: {if $a}I'm #winning {/if}", "#winning", "mira: {if $a}I'm \\#winning {/if}")]
    public void Parse_TagInsideOpenSpanOrBranch_ReportsTextAfterTagOnly(string line, string tag, string fixedLine)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1015", $"If `{tag}` is part of what mira says, put a backslash before the `#`:\n{fixedLine}"), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("-> [b]Email @home[/b] now")]
    [InlineData("-> {if $a}Email @home{/if} now")]
    public void Parse_ModifierLookalikeInsideOpenSpanOrBranch_IsUnexpected(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1040", "I didn't expect `@home` here."), (diagnostic.Code, diagnostic.Message));
    }

    [Theory]
    [InlineData("-> Email me @home")]
    [InlineData("-> [b]Email @home[/b] now")]
    [InlineData("-> {if $a}Email @home{/if} now")]
    public void Parse_UnknownModifierWhereTextWas_OffersTheEscape(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1040", "If `@home` is text, put a backslash before the `@`."), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("mira: Go #team!")]
    [InlineData("Go #team!")]
    [InlineData("-> Go #team!")]
    public void Parse_MalformedTagNameAfterText_OffersTheEscape(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1043", "Write `#team_`.\nIf `#team!` is text, put a backslash before the `#`."), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("-> Go @once #team!")]
    [InlineData("mira: Hi #thought #team!")]
    [InlineData("@call kitchen.door #team!")]
    public void Parse_MalformedTagNameThatIsNotText_OffersOnlyTheFix(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1043", "Write `#team_`."), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("-> Email me @once @home")]
    [InlineData("-> Email me @once @once")]
    [InlineData("-> Email me @if true @if true")]
    public void Parse_UnexpectedAfterAModifier_OffersNoHelp(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1040", null), (diagnostic.Code, diagnostic.Help));
    }

    [Theory]
    [InlineData("mira: [b]x #thought", "PIB1010")]
    [InlineData("mira: {if $a}x #thought", "PIB1013")]
    [InlineData("-> [b]x @once", "PIB1010")]
    [InlineData("-> {if $a}x @once", "PIB1013")]
    public void Parse_ValidTagOrModifierAfterOpenSpanOrBranch_StillReportsUnclosed(string line, string code)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Assert.Equal(code, Assert.Single(SyntaxTree.Parse(source).Diagnostics).Code);
    }

    [Theory]
    [InlineData("mira: Hi{w}", typeof(InputWaitSyntax))]
    [InlineData("mira: Hi{pause 0.5}", typeof(PauseSyntax))]
    public void Parse_WaitOrPausePoint_MakesItsOwnNode(string line, Type expected)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        var text = (TextLineSyntax)SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        Assert.IsType(expected, text.Content[^1]);
    }

    [Theory]
    [InlineData("-> Knock{w}", "{w}")]
    [InlineData("-> Knock{pause 0.5}", "{pause}")]
    [InlineData("-> Knock{page}", "{page}")]
    public void Parse_WaitOrPauseInOption_NamesThePointWritten(string line, string point)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n    mira: Hi\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(($"An option's text can't contain `{point}`.", "PIB1016"), (diagnostic.Message, diagnostic.Code));
    }

    [Theory]
    [InlineData("w", "0.5", "To pause for that long, write `{pause 0.5}`.")]
    [InlineData("w", "$delay", "To pause for that long, write `{pause $delay}`.")]
    [InlineData("page", "2", "Remove `2`.")]
    [InlineData("br", "(1 + 1)", "Remove `(1 + 1)`.")]
    [InlineData("w", "-1", "To pause for that long, write `{pause -1}`.")]
    [InlineData("page", "-", "Remove `-`.")]
    public void Parse_ValueAfterPointThatTakesNone_NamesThePointAndOffersAFix(string point, string value, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{{{point} {value}}} there\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1018", $"`{{{point}}}` takes no value, so I can't use `{value}` after it.", help), (diagnostic.Code, diagnostic.Message, diagnostic.Help));
    }

    [Theory]
    [InlineData("{p}", "To break the page, write `{page}`.")]
    [InlineData("{p 0.5}", "To break the page, write `{page}`. To pause for that long, write `{pause 0.5}`.")]
    [InlineData("{p -1}", "To break the page, write `{page}`. To pause for that long, write `{pause -1}`.")]
    public void Parse_OldPageBreak_NamesThePointAndOffersThePageBreak(string point, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{point} there\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Diagnostic diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(("PIB1017", $"I don't know what `{point}` means.", help), (diagnostic.Code, diagnostic.Message, diagnostic.Help));
    }

    [Fact]
    public void Parse_OldPageBreakInOption_IsReportedOnce()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n-> Knock{p}\n    mira: Hi\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal("PIB1017", diagnostic.Code);
    }

    [Theory]
    [InlineData("{speed 2 3}")]
    [InlineData("{pause 1 2}")]
    public void Parse_SecondValueAfterPointThatTakesOne_IsUnexpectedNotPointTakesNoValue(string point)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{point} there\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal("PIB1040", diagnostic.Code);
    }

    [Fact]
    public void Parse_PauseWithoutDuration_ReportsMissingValueAndKeepsThePoint()
    {
        var source = new SourceText("story.pib", "== kitchen.door\nmira: Hi{pause}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var pause = (PauseSyntax)((TextLineSyntax)tree.Root.Nodes[0].Body[0]).Content[^1];
        Assert.Equal(("PIB1046", true), (Assert.Single(tree.Diagnostics).Code, pause.Duration is ErrorExpressionSyntax));
    }

    [Theory]
    [InlineData("@var door_open = false", "door_open", "@var $door_open = false")]
    [InlineData("@var door_open: bool = false // note", "door_open", "@var $door_open: bool = false")]
    [InlineData("@var .attempts = 0", ".attempts", "@var $.attempts = 0")]
    public void Parse_VariableDeclarationWithoutDollar_ReportsItOnceAndKeepsTheDeclaration(string line, string name, string corrected)
    {
        var source = new SourceText("story.pib", $"{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var declaration = (VariableDeclarationSyntax)tree.Root.Declarations[0];
        Diagnostic diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(("PIB1049", $"Write `{corrected}`.", name), (diagnostic.Code, diagnostic.Help, declaration.Variable.Name));
        Assert.IsNotType<ErrorExpressionSyntax>(declaration.Value);
    }

    [Fact]
    public void Parse_SetWithoutDollar_ReportsItOnceAndKeepsTheStatement()
    {
        var source = new SourceText("story.pib", "== a.b\n@set door_open = true // note\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var set = (SetStatementSyntax)tree.Root.Nodes[0].Body[0];
        Diagnostic diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(("PIB1049", "Write `@set $door_open = true`.", "door_open"), (diagnostic.Code, diagnostic.Help, set.Variable.Name));
    }

    [Theory]
    [InlineData("mira: [b]unclosed")]
    [InlineData("mira: {$unclosed")]
    [InlineData("-> [b]unclosed")]
    public void Parse_UnclosedAtEndOfLineWithTrailingWhitespace_StaysInsideTheLine(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}  \t\n");

        StatementSyntax statement = SyntaxTree.Parse(source).Root.Nodes[0].Body[0];

        Assert.All(NodeFields.DescendantsAndSelf(statement), node => Assert.True(node.Span.End <= statement.Span.End, $"{node.GetType().Name} at {node.Span} ends after its line."));
    }

    [Theory]
    [InlineData("mira: Hello there. // said warmly", "mira: Hello there.", "said warmly")]
    [InlineData("The kitchen smells of coffee.//touching", "The kitchen smells of coffee.", "touching")]
    [InlineData("mira: Hi {$name}// touching a closer", "mira: Hi {$name}", "touching a closer")]
    [InlineData("mira: Hi #thought #box:phone // after tags", "mira: Hi #thought #box:phone", "after tags")]
    [InlineData("mira (sad): // still sad", "mira (sad):", "still sad")]
    [InlineData("-> Go // polite", "-> Go", "polite")]
    [InlineData("-> Go @if $a @once // after modifiers", "-> Go @if $a @once", "after modifiers")]
    [InlineData("-> Go #thought // after a tag", "-> Go #thought", "after a tag")]
    [InlineData("mira: Banner //// longer", "mira: Banner", "// longer")]
    public void Parse_CommentAtEndOfLine_EndsTheLineBeforeIt(string line, string content, string comment)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
        Comment found = Assert.Single(tree.Comments);
        Assert.Equal(("== kitchen.door\n" + content).Length, tree.Root.Nodes[0].Body[0].Span.End);
        Assert.Equal((comment, false), (found.Text, found.OnOwnLine));
    }

    [Fact]
    public void Parse_TagValueFollowedByOtherSpaceSeparator_EndsAtIt()
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi #box:phone{Spaces.Ideographic}#thought\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var line = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Equal(["box:phone", "thought:"], line.Tags.Select(tag => $"{tag.Name}:{tag.Value}"));
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Parse_OtherSpaceSeparators_AreSpeakerSpacingAndTrimmedFromText()
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nguide{Spaces.NoBreak}:{Spaces.Ideographic}Bonjour.{Spaces.NarrowNoBreak}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var line = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Equal(("guide", "Bonjour."), (line.Speaker!.Name!.Text, ((TextRunSyntax)Assert.Single(line.Content)).Text));
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Parse_DashFollowedByOtherSpaceSeparator_IsAnAlternative()
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n@cycle\n    -{Spaces.Ideographic}mira: Hi\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var variation = (VariationStatementSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Equal((1, 0), (Assert.Single(variation.Alternatives).Body.Count, tree.Diagnostics.Count));
    }

    [Fact]
    public void Parse_DisplayNameAndCommentAroundOtherSpaceSeparators_AreTrimmed()
    {
        var source = new SourceText("story.pib", $"@actor mira\n    name:{Spaces.Ideographic}Mira{Spaces.NoBreak}//{Spaces.Ideographic}said warmly{Spaces.NoBreak}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var actor = (ActorDeclarationSyntax)tree.Root.Declarations[0];
        Assert.Equal(("Mira", "said warmly"), (actor.DisplayName, Assert.Single(tree.Comments).Text));
    }

    [Fact]
    public void Parse_CommentAfterTextInAlternative_EndsTheAlternativeBeforeIt()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n@sequence\n    - mira: Hi // said warmly\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var variation = (VariationStatementSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Equal("@sequence\n    - mira: Hi".Length, variation.Alternatives[0].Span.End - variation.Span.Start);
    }

    [Theory]
    [InlineData("mira: Visit https:\\//example.com", "Visit https://example.com")]
    [InlineData("mira: A \\//B", "A //B")]
    public void Parse_EscapedSlashes_AreTextNotAComment(string line, string text)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var content = (TextRunSyntax)((TextLineSyntax)tree.Root.Nodes[0].Body[0]).Content[0];
        Assert.Equal((text, 0, 0), (content.Text, tree.Diagnostics.Count, tree.Comments.Count));
    }

    [Theory]
    [InlineData("mira: Hi {f(\"a//b\")}")]
    [InlineData("mira: Hi #link:https://example.com")]
    [InlineData("-> Go @if f(\"a//b\")")]
    [InlineData("@jolt \"a//b\"")]
    public void Parse_SlashesInStringOrTagValue_AreNotAComment(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal((0, 0), (tree.Diagnostics.Count, tree.Comments.Count));
    }

    [Theory]
    [InlineData("mira: Hi {$name // }", "PIB1013")]
    [InlineData("mira: Hi {if $a}yes // {/if}", "PIB1013")]
    [InlineData("mira: Hi [wave 1 // ]x[/wave]", "PIB1013")]
    [InlineData("mira: [b]bold // [/b]", "PIB1010")]
    public void Parse_CommentInsideBracketsOrSpan_LeavesItUnclosed(string line, string code)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal((code, 1), (Assert.Single(tree.Diagnostics).Code, tree.Comments.Count));
    }

    [Theory]
    [InlineData("mira: Hi {$name // }", "Close it on the same line. The `//` on this line starts a comment that runs to the end of it, so put the closer before the `//`.")]
    [InlineData("mira: [b]bold // [/b]", "Close it on the same line with `[/b]`. The `//` on this line starts a comment that runs to the end of it, so put the closer before the `//`.")]
    [InlineData("mira: Hi {$name", "Close it on the same line.")]
    [InlineData("mira: [b]bold", "Close it on the same line with `[/b]`.")]
    public void Parse_UnclosedWhereACommentMayHaveTakenTheCloser_SaysSoInTheHelp(string line, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(help, diagnostic.Help);
    }

    [Theory]
    [InlineData("name: Mira Okafor // the cast list", "Mira Okafor")]
    [InlineData("name: Mira//Okafor", "Mira")]
    [InlineData("name: Mira \\ Okafor", "Mira \\ Okafor")]
    public void Parse_CommentAfterDisplayName_EndsTheName(string property, string displayName)
    {
        var source = new SourceText("story.pib", $"@actor mira\n    {property}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var actor = (ActorDeclarationSyntax)tree.Root.Declarations[0];
        Assert.Equal((displayName, 0), (actor.DisplayName, tree.Diagnostics.Count));
    }

    [Theory]
    [InlineData("@actor mira\n    name: Mira // the cast list\n", "@actor mira\n    name: Mira")]
    [InlineData("@narration\n    pose: idle // the default\n", "@narration\n    pose: idle")]
    public void Parse_SpeakerBlockEndingInComment_EndsBeforeIt(string text, string spanned)
    {
        var source = new SourceText("story.pib", text);

        TextSpan span = SyntaxTree.Parse(source).Root.Declarations[0].Span;

        Assert.Equal(spanned, text.Substring(span.Start, span.Length));
    }

    [Theory]
    [InlineData("mira: Hi. // a greeting", "mira: Hi.")]
    [InlineData("-> Go // an option", "-> Go")]
    [InlineData("@jump kitchen.door // a jump", "@jump kitchen.door")]
    [InlineData("@jump \"//\" // a string", "@jump \"//\"")]
    public void Parse_LineOutsideNodeWithComment_IsReportedUpToTheComment(string line, string reported)
    {
        var source = new SourceText("story.pib", $"{line}\n");

        TextSpan span = Assert.Single(SyntaxTree.Parse(source).Diagnostics, diagnostic => diagnostic.Code is "PIB1022").Location.Span;

        Assert.Equal(reported, line.Substring(span.Start, span.Length));
    }

    [Theory]
    [InlineData("mira (happy): Hi.", new[] { "happy" }, true)]
    [InlineData("mira (happy, shake, pan_left): Hi.", new[] { "happy", "shake", "pan_left" }, true)]
    [InlineData("mira(shake ,happy) : Hi.", new[] { "shake", "happy" }, true)]
    [InlineData("mira (happy, shake):", new[] { "happy", "shake" }, false)]
    [InlineData("mira: Hi.", new string[0], true)]
    [InlineData("mira（happy、shake）：Hi.", new[] { "happy", "shake" }, true)]
    [InlineData("mira (happy，shake) : Hi.", new[] { "happy", "shake" }, true)]
    [InlineData("mira（happy） : Hi.", new[] { "happy" }, true)]
    [InlineData("mira (happy, shake)：", new[] { "happy", "shake" }, false)]
    public void Parse_SpeakerValues_AreTheNamesInTheParenthesisInTheOrderWritten(string line, string[] values, bool hasText)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var text = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Equal(values, text.Speaker!.Values.Select(value => value.Text));
        Assert.Equal((hasText, 0), (text.Content.Count > 0, tree.Diagnostics.Count));
    }

    [Theory]
    [InlineData("mira (to Rex): Hi.")]
    [InlineData("mira (happy,): Hi.")]
    [InlineData("mira (): Hi.")]
    [InlineData("mira (happy) shake: Hi.")]
    public void Parse_ParenthesisThatIsNotAListOfNames_IsNarrationForTheBinderToReport(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var text = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Null(text.Speaker);
    }

    [Theory]
    [InlineData("(shake): The floor rumbles.", new[] { "shake" }, true)]
    [InlineData("(shake, orbit_scene):", new[] { "shake", "orbit_scene" }, false)]
    [InlineData("( shake ,orbit ) : Spacing doesn't matter.", new[] { "shake", "orbit" }, true)]
    [InlineData("（shake、orbit）：The floor rumbles.", new[] { "shake", "orbit" }, true)]
    [InlineData("（shake）：", new[] { "shake" }, false)]
    public void Parse_NarrationParenthesis_HasValuesAndNoName(string line, string[] values, bool hasText)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var text = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Null(text.Speaker!.Name);
        Assert.Equal(values, text.Speaker.Values.Select(value => value.Text));
        Assert.Equal((hasText, 0), (text.Content.Count > 0, tree.Diagnostics.Count));
    }

    [Theory]
    [InlineData("(to the room): Hi.")]
    [InlineData("(aside) more: Hi.")]
    [InlineData("(Note: this is an aside) and more.")]
    [InlineData("(shake):Hi.")]
    [InlineData("\\(shake): Hi.")]
    [InlineData("\\（shake）：Hi.")]
    [InlineData("（to the room）：Hi.")]
    public void Parse_ParenthesisAtTheStartThatIsNotNarrationsValues_IsNarration(string line)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var text = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Null(text.Speaker);
    }

    [Fact]
    public void Parse_EscapedFullWidthColon_IsNarrationThatShowsTheColon()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n他说\\：“你好。”\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var text = (TextLineSyntax)tree.Root.Nodes[0].Body[0];
        Assert.Equal((null, "他说：“你好。”", 0), (text.Speaker, ((TextRunSyntax)Assert.Single(text.Content)).Text, tree.Diagnostics.Count));
    }

    [Fact]
    public void Parse_NarrationBlock_TakesTheLinesOfAnActorsBlock()
    {
        var source = new SourceText("story.pib", "@narration\n    name: Narrator\n    no pose\n    camera += orbit_scene\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var narration = (NarrationDeclarationSyntax)tree.Root.Declarations[0];
        Assert.Equal(["pose:Drop", "camera:Add"], narration.Channels.Select(edit => $"{edit.Channel.Text}:{edit.Edit}"));
        Assert.Equal(("Narrator", 0), (narration.DisplayName, tree.Diagnostics.Count));
    }

    [Fact]
    public void Parse_NarrationWithNoBlock_IsReportedAsMissingItsBlock()
    {
        var source = new SourceText("story.pib", "@narration\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1004", Assert.Single(tree.Diagnostics).Code);
    }

    [Theory]
    [InlineData("== kitchen.door\n@if $open foo\n")]
    [InlineData("== kitchen.door\n@once foo\n")]
    [InlineData("== kitchen.door\n@sequence foo\n")]
    [InlineData("@actor mira foo\n")]
    [InlineData("@narration foo\n")]
    public void Parse_OpenerWithLeftoverTextAndNoBlock_ReportsOnlyTheLeftover(string text)
    {
        var source = new SourceText("story.pib", text);

        Assert.Equal("PIB1040", Assert.Single(SyntaxTree.Parse(source).Diagnostics).Code);
    }

    [Fact]
    public void Parse_ChannelDeclarations_HaveValuesOrNone()
    {
        var source = new SourceText("story.pib", "@channel pose: neutral, happy\n@channel effect\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var channels = tree.Root.Declarations.Cast<ChannelDeclarationSyntax>().ToArray();
        Assert.Equal(["pose", "effect"], channels.Select(channel => channel.Name.Text));
        Assert.Equal([2, 0], channels.Select(channel => channel.Values.Count));
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Parse_ActorBlock_ReadsReplacingAddingAndDroppingLinesInOrder()
    {
        var source = new SourceText("story.pib", "@actor mira\n    no camera, effect\n    name: Mira\n    pose: idle, scanning // two poses\n    mood += calm\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var actor = (ActorDeclarationSyntax)tree.Root.Declarations[0];
        Assert.Equal(
            ["camera:Drop:", "effect:Drop:", "pose:Replace:idle,scanning", "mood:Add:calm"],
            actor.Channels.Select(edit => $"{edit.Channel.Text}:{edit.Edit}:{string.Join(',', edit.Values.Select(value => value.Text))}"));
        Assert.Equal(("Mira", 0), (actor.DisplayName, tree.Diagnostics.Count));
    }

    [Fact]
    public void Parse_ActorBlockLineThatIsNoneOfThose_IsReportedAndSkipped()
    {
        var source = new SourceText("story.pib", "@actor mira\n    mood\n    pose: idle\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var actor = (ActorDeclarationSyntax)tree.Root.Declarations[0];
        Assert.Equal((1, 1), (tree.Diagnostics.Count, actor.Channels.Count));
    }

    [Theory]
    [InlineData("mood", "`mood`")]
    [InlineData("@set $x = 1", "`@set`")]
    [InlineData("-> Go", "`->`")]
    [InlineData("== kitchen.door", "`==`")]
    [InlineData("- Hm.", "`-`")]
    public void Parse_ActorBlockLineThatIsNoneOfThose_NamesItsFirstWord(string line, string word)
    {
        var source = new SourceText("story.pib", $"@actor mira\n    {line}\n");

        Assert.Equal($"I didn't expect {word} here.", Assert.Single(SyntaxTree.Parse(source).Diagnostics).Message);
    }

    [Fact]
    public void Parse_OverIndentedActorBlockLine_IsReportedAndRead()
    {
        var source = new SourceText("story.pib", "@actor mira\n    name: Mira\n        pose: idle\n    mood += calm\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        var actor = (ActorDeclarationSyntax)tree.Root.Declarations[0];
        Assert.Equal("PIB1003", Assert.Single(tree.Diagnostics).Code);
        Assert.Equal(["pose", "mood"], actor.Channels.Select(edit => edit.Channel.Text));
    }

    [Fact]
    public void Parse_DisplayNameThatIsOnlyAComment_ReportsMissingName()
    {
        var source = new SourceText("story.pib", "@actor mira\n    name: // Mira\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal(("PIB1046", 1), (Assert.Single(tree.Diagnostics).Code, tree.Comments.Count));
    }

    [Theory]
    [InlineData("mira: Hi /// note", 1, 0)]
    [InlineData("-> Go /// note", 1, 0)]
    [InlineData("@jump kitchen.door /// note", 1, 0)]
    [InlineData("mira: Hi //// banner", 0, 1)]
    [InlineData("mira: Hi ///// banner", 0, 1)]
    public void Parse_NoteAfterContent_IsReportedAndNotRecordedUnlessItHasFourSlashes(string line, int diagnostics, int comments)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal((diagnostics, comments), (tree.Diagnostics.Count, tree.Comments.Count));
    }

    [Theory]
    [InlineData("@jump \"abc", "PIB1041")]
    [InlineData("@jump 1x", "PIB1042")]
    [InlineData("@foo #a!b", "PIB1043")]
    [InlineData("mira: {pause \"abc}", "PIB1041")]
    [InlineData("mira: #a!b more", "PIB1043")]
    [InlineData("mira: [///", "PIB1005")]
    public void Parse_TokenTheLexerReported_IsNotReportedAgain(string line, string code)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        Assert.Equal(code, Assert.Single(SyntaxTree.Parse(source).Diagnostics).Code);
    }

    [Fact]
    public void Parse_SeparateLexerProblemsOnOneLine_AreEachReported()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n@foo \"\\n\" 1x\n");

        Assert.Equal(["PIB1014", "PIB1042"], SyntaxTree.Parse(source).Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Theory]
    [InlineData("{if $a}x{else}y{else}z{/if}", "`{else}`")]
    [InlineData("{if $a}x{else}y{elif $b}z{/if}", "`{elif $b}`")]
    [InlineData("[b]x{else}y[/b]", "`{else}`")]
    [InlineData("[b]x{/if}y[/b]", "`{/if}`")]
    public void Parse_MisplacedBranchKeyword_IsUnexpectedNotAMissingCloser(string text, string keyword)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: {text}\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1040", $"I didn't expect {keyword} here."), (diagnostic.Code, diagnostic.Message));
    }

    [Fact]
    public void Parse_SpanCutByItsBranchsEnd_IsUnclosed()
    {
        var source = new SourceText("story.pib", "== kitchen.door\nmira: {if $a}x[b]y{else}z[/b]{/if}\n");

        Assert.Equal("PIB1010", Assert.Single(SyntaxTree.Parse(source).Diagnostics).Code);
    }

    [Theory]
    [InlineData("[i][b]x[/i#x]", "PIB1043")]
    [InlineData("[i][c][b]x[/i \"abc]", "PIB1041")]
    public void Parse_MalformedCloseOfAnOuterSpan_IsLexedOnce(string text, string code)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: {text}\n");

        Assert.Equal(code, Assert.Single(SyntaxTree.Parse(source).Diagnostics).Code);
    }

    [Fact]
    public void Parse_VariablesBeforeHeaderOrEndOfFile_BelongToTheFile()
    {
        var source = new SourceText("story.pib", "== kitchen.door\nmira: Hi\n@var $a = 1\n// A comment.\n\n@var $b = 2\n== kitchen.window\nmira: Bye\n@var $c = 3\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(
            [nameof(NodeSyntax), nameof(VariableDeclarationSyntax), nameof(VariableDeclarationSyntax), nameof(NodeSyntax), nameof(VariableDeclarationSyntax)],
            tree.Root.Members.Select(member => member.GetType().Name));
        Assert.All(tree.Root.Nodes, node => Assert.Single(node.Body));
    }

    [Fact]
    public void Parse_VariableWithNodeLinesAfterIt_IsReportedOnceAndTheLinesStayInTheNode()
    {
        var source = new SourceText("story.pib", "== kitchen.door\n@var $a = 1\nmira: Hi\n== kitchen.window\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1021", Assert.Single(tree.Diagnostics).Code);
        Assert.Single(tree.Root.Nodes[0].Body);
    }

    [Fact]
    public void Parse_MarkersTypedThroughInputMethodInCode_ReadAsTheMarkers()
    {
        var source = new SourceText("story.pib", "== a.b\n@jump ime。case\n@pause ０。５s\n@if has_item（“key”）\n    mira: Found it.\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        IReadOnlyList<StatementSyntax> body = tree.Root.Nodes[0].Body;
        var condition = (CallExpressionSyntax)((IfStatementSyntax)body[2]).Condition;
        Assert.Equal(
            ("ime.case", 0.5m, "has_item", "key"),
            (((JumpStatementSyntax)body[0]).Target.Text, ((DurationLiteralSyntax)((PauseStatementSyntax)body[1]).Duration).Seconds, condition.Function.Text, ((StringLiteralSyntax)condition.Arguments[0]).Value));
        Assert.Single(((IfStatementSyntax)body[2]).Body);
        Assert.All(tree.Diagnostics, diagnostic => Assert.Equal("PIB1044", diagnostic.Code));
    }

    [Fact]
    public void Parse_MarkerTypedThroughInputMethod_NamesItAndGivesTheLineHalfWidth()
    {
        var source = new SourceText("story.pib", "== a.b\n@set ＄met ＝ true\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal(
            [
                ("PIB1044", "I read `＄` (U+FF04) as `$`, but markers are typed half-width.", "Write `@set $met = true`, typing markers in your input method's direct (half-width) mode."),
                ("PIB1044", "I read `＝` (U+FF1D) as `=`, but markers are typed half-width.", "Write `@set $met = true`, typing markers in your input method's direct (half-width) mode."),
            ],
            tree.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Message, diagnostic.Help)));
    }

    [Fact]
    public void Parse_MarkerTypedThroughInputMethodBeforeAnotherProblem_ReportsBoth()
    {
        var source = new SourceText("story.pib", "== a.b\n@set ＄met ＝\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal(["PIB1044", "PIB1044", "PIB1046"], tree.Diagnostics.OrderBy(diagnostic => diagnostic.Location.Span.Start).Select(diagnostic => diagnostic.Code));
    }

    [Theory]
    [InlineData("@var $a = 1", "Move it above the first `==` line, or below the node's last line. To change a variable here, use `@set`.")]
    [InlineData("@command knock()", "Only `@var` can go between nodes. Move this above the first `==` line, or into another file.")]
    public void Parse_DeclarationInNode_SaysWhereItsKindCanGo(string declaration, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{declaration}\nmira: Hi\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1021", $"`{declaration.Split(' ')[0]}` can't go inside a node.", help), (diagnostic.Code, diagnostic.Message, diagnostic.Help));
    }
}
