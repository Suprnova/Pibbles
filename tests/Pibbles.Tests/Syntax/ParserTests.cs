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
    public void Parse_ValueAfterPointThatTakesNone_NamesThePointAndOffersAFix(string point, string value, string help)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\nmira: Hi{{{point} {value}}} there\n");

        Diagnostic diagnostic = Assert.Single(SyntaxTree.Parse(source).Diagnostics);

        Assert.Equal(("PIB1018", $"`{{{point}}}` takes no value, so I can't use `{value}` after it.", help), (diagnostic.Code, diagnostic.Message, diagnostic.Help));
    }

    [Theory]
    [InlineData("{p}", "To break the page, write `{page}`.")]
    [InlineData("{p 0.5}", "To break the page, write `{page}`. To pause for that long, write `{pause 0.5}`.")]
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
    [InlineData("mira (happy): Hi.", new[] { "happy" }, true)]
    [InlineData("mira (happy, shake, pan_left): Hi.", new[] { "happy", "shake", "pan_left" }, true)]
    [InlineData("mira(shake ,happy) : Hi.", new[] { "shake", "happy" }, true)]
    [InlineData("mira (happy, shake):", new[] { "happy", "shake" }, false)]
    [InlineData("mira: Hi.", new string[0], true)]
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

    [Fact]
    public void Parse_DisplayNameThatIsOnlyAComment_ReportsMissingName()
    {
        var source = new SourceText("story.pib", "@actor mira\n    name: // Mira\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal(("PIB1046", 1), (Assert.Single(tree.Diagnostics).Code, tree.Comments.Count));
    }

    [Theory]
    [InlineData("mira: Hi /// note", 1)]
    [InlineData("-> Go /// note", 1)]
    [InlineData("mira: Hi //// banner", 0)]
    [InlineData("mira: Hi ///// banner", 0)]
    public void Parse_NoteAfterContent_IsReportedUnlessItHasFourSlashes(string line, int diagnostics)
    {
        var source = new SourceText("story.pib", $"== kitchen.door\n{line}\n");

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal((diagnostics, 1), (tree.Diagnostics.Count, tree.Comments.Count));
    }
}
