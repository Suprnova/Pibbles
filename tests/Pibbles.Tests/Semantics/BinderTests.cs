using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public class BinderTests
{
    /// <summary>A variable of each type to use as an operand, and one of each type to assign a result to.</summary>
    private const string Operands = """
        @enum position: left, right
        @var $n = 1
        @var $d = 1s
        @var $s = "a"
        @var $b = true
        @var $e: position = left
        @var $out_number = 0
        @var $out_duration = 0s
        @var $out_string = ""
        @var $out_bool = false

        == test.node

        """;

    /// <summary>Every combination the operator types in <c>docs/language/reference.md</c> allow, with the type of the result.</summary>
    public static TheoryData<string, string> ValidOperations { get; } = new()
    {
        { "not $b", "bool" },
        { "$b and $b", "bool" },
        { "$b or $b", "bool" },
        { "$n == $n", "bool" },
        { "$s != $s", "bool" },
        { "$e == $e", "bool" },
        { "$b == $b", "bool" },
        { "$d == $n", "bool" },
        { "$n < $n", "bool" },
        { "$d >= $d", "bool" },
        { "$d > $n", "bool" },
        { "$n <= $d", "bool" },
        { "$n + $n", "number" },
        { "$d + $d", "duration" },
        { "$n + $d", "duration" },
        { "$s + $s", "string" },
        { "$n - $n", "number" },
        { "$d - $n", "duration" },
        { "$n * $n", "number" },
        { "$d * $n", "duration" },
        { "$n * $d", "duration" },
        { "$n / $n", "number" },
        { "$d / $n", "duration" },
        { "$d / $d", "number" },
        { "$n % $n", "number" },
        { "-$n", "number" },
        { "-$d", "duration" },
    };

    [Theory]
    [MemberData(nameof(ValidOperations))]
    public void Compile_ValidOperation_HasResultType(string expression, string type) =>
        Assert.Empty(Compile($"{Operands}@set $out_{type} = {expression}\n").Diagnostics);

    /// <summary>Combinations the operator types don't allow, each assigned to a variable of the type the operator would give.</summary>
    public static TheoryData<string, string> InvalidOperations { get; } = new()
    {
        { "not $n", "bool" },
        { "$n and $b", "bool" },
        { "$s or $b", "bool" },
        { "$n == $s", "bool" },
        { "$b != $n", "bool" },
        { "$e == $s", "bool" },
        { "$s < $s", "bool" },
        { "$e < $e", "bool" },
        { "$b >= $b", "bool" },
        { "$b + $b", "number" },
        { "$n + $s", "string" },
        { "$e + $e", "number" },
        { "$s - $s", "string" },
        { "$d * $d", "duration" },
        { "$s * $n", "string" },
        { "$n / $d", "number" },
        { "$d % $n", "duration" },
        { "$d % $d", "number" },
        { "-$b", "bool" },
        { "-$s", "string" },
    };

    [Theory]
    [MemberData(nameof(InvalidOperations))]
    public void Compile_InvalidOperation_ReportsOperatorTypesOnly(string expression, string type) =>
        Assert.Equal(["PIB2033"], Compile($"{Operands}@set $out_{type} = {expression}\n").Diagnostics.Select(diagnostic => diagnostic.Code));

    public static TheoryData<string, string, string?> Messages { get; } = new()
    {
        {
            "@prefix kitchen\n\n== .door\n@if visits(.dor) > 0\n    Hm.\n",
            "I can't find a node called `.dor`.",
            "Did you mean `.door`?"
        },
        {
            "@prefix kitchen\n\n== .door #was:.front_door\n@if visits(.front_door) > 0\n    Hm.\n",
            "`.front_door` is an old name of `.door`.",
            "Use the current name: `.door`."
        },
        {
            "@var $tries = 0\n\n== a.b\n@if $tries\n    Hm.\n",
            "`$tries` is a `number`, but a condition has to be true or false.",
            "Did you mean `$tries > 0`?"
        },
        {
            "@var $has_key = false\n\n== a.b\n@if has_key\n    Hm.\n",
            "I don't know what `has_key` means here.",
            "Did you mean `$has_key`?"
        },
        {
            "@function has_item(id: string) -> bool\n\n== a.b\n@if has_item(crowbar)\n    Hm.\n",
            "I don't know what `crowbar` means here.",
            "If it's text, put it in quotes: `\"crowbar\"`."
        },
        {
            "@enum position: left, right\n@var $where: position = lfet\n",
            "`lfet` isn't a `position`.",
            "Did you mean `left`?"
        },
        {
            "@enum position: left, right\n@var $where: position = middle\n",
            "`middle` isn't a `position`.",
            "Use one of `left` or `right`."
        },
        {
            "@enum position: left, center, right\n@var $where: position = 3\n",
            "`$where` holds a `position`, but this is a `number`.",
            "Use one of `left`, `center` or `right`."
        },
        {
            "@function near(who: string, where: string) -> bool\n\n== a.b\n@if near()\n    Hm.\n",
            "`near()` needs values for `who` and `where`.",
            "Add them in the brackets, in order."
        },
        {
            "@function is_night() -> bool\n\n== a.b\n@if is_night(1)\n    Hm.\n",
            "`is_night()` takes no arguments.",
            "Remove the extra ones."
        },
        {
            "@var $has_key = false\n\n== a.b\n@set $has_kye = true\n",
            "I don't know a variable called `$has_kye`.",
            "Did you mean `$has_key`?"
        },
        {
            "@var $count = 0\n\n== a.b\n@set $count += 1s\n",
            "`$count` holds a `number`, but this is a `duration`.",
            "Write a number, like `1` or `0.5`."
        },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Compile_Problem_ReportsMessageAndHelp(string text, string message, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal((message, help), (diagnostic.Message, diagnostic.Help));
    }

    [Fact]
    public void Compile_ProblemInsideBrokenExpression_ReportsOnce()
    {
        var compilation = Compile("@var $n = 0\n\n== a.b\n@if ($missing + 1) * 2 > $n and not $missing\n    Hm.\n");

        Assert.Equal(["PIB2030", "PIB2030"], compilation.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    private static Compilation Compile(string text) => Compilation.Create([new SourceText("story.pib", text)]);
}
