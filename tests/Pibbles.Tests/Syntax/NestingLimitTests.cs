using Pibbles.Syntax;
using Pibbles.Tests.Runtime;

namespace Pibbles.Tests.Syntax;

public class NestingLimitTests
{
    private const int Limit = Parser.MaxNesting;

    private static readonly Dictionary<string, Func<int, string>> Shapes = new()
    {
        ["blocks"] = depth => string.Concat(Enumerable.Range(0, depth).Select(level => $"{new string(' ', level)}@if $key\n")) + $"{new string(' ', depth)}Hi.\n",
        ["markup"] = depth => $"{Repeat("[shout]", depth)}Hi.{Repeat("[/shout]", depth)}\n",
        ["branches"] = depth => $"{Repeat("{if $key}", depth)}Hi.{Repeat("{/if}", depth)}\n",
        ["parentheses"] = depth => $"@set $count = {Repeat("(", depth)}1{Repeat(")", depth)}\n",
        ["calls"] = depth => $"@set $count = {Repeat("f(", depth)}1{Repeat(")", depth)}\n",
        ["nots"] = depth => $"@if {Repeat("not ", depth)}$key\n Hi.\n",
        ["negations"] = depth => $"@set $count = {Repeat("- ", depth)}1\n",
        ["sums"] = depth => $"@set $count = 1{Repeat(" + 1", depth)}\n",
        ["conjunctions"] = depth => $"@if $key{Repeat(" and $key", depth)}\n Hi.\n",
        ["chain in parentheses"] = depth => $"@set $count = (1{Repeat(" + 1", depth - 1)})\n",
    };

    public static TheoryData<string> ShapeNames { get; } = [.. Shapes.Keys];

    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void Parse_ShapeAtTheLimit_ReportsNothing(string shape)
    {
        var source = new SourceText("story.pib", Story(shape, Limit));

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void Parse_ShapePastTheLimit_ReportsItOnce(string shape)
    {
        var source = new SourceText("story.pib", Story(shape, Limit + 1));

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1062", Assert.Single(tree.Diagnostics).Code);
    }

    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void Play_ShapeAtTheLimit_Finishes(string shape)
    {
        Game game = Game.Of(Story(shape, Limit), functions => functions.Add("f", (decimal n) => n));

        string[] run = game.Play("t.n");

        Assert.Equal("End", run[^1]);
    }

    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void Parse_ShapeFarPastTheLimit_ReportsItOnce(string shape)
    {
        var source = new SourceText("story.pib", Story(shape, 5_000));

        SyntaxTree tree = SyntaxTree.Parse(source);

        Assert.Equal("PIB1062", Assert.Single(tree.Diagnostics).Code);
    }

    [Fact]
    public void Parse_UnexpectedIndentationPastTheLimit_ReportsItOnce()
    {
        string text = string.Concat(Enumerable.Range(1, Limit + 10).Select(level => $"{new string(' ', level)}Hi.\n"));

        SyntaxTree tree = SyntaxTree.Parse(new("story.pib", $"== t.n\n{text}"));

        Assert.Single(tree.Diagnostics, diagnostic => diagnostic.Code is "PIB1062");
    }

    private static string Story(string shape, int depth) => $"@function f(n: number) -> number\n== t.n\n@set $key = true\n{Shapes[shape](depth)}";

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
}
