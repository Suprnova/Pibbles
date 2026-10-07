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
        Assert.Empty(Diagnose($"{Operands}@set $out_{type} = {expression}\n"));

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
        Assert.Equal(["PIB2033"], Diagnose($"{Operands}@set $out_{type} = {expression}\n").Select(diagnostic => diagnostic.Code));

    public static TheoryData<string, string, string?> Messages { get; } = new()
    {
        {
            "== a.b\nVery [speed 0.3]slowly[/speed] there.\n",
            "I don't know markup called `speed`.",
            "Did you mean `{speed}`?"
        },
        {
            "== a.b\nWait.{w (0.3s - 0.1s - 0.2s)} Done.\n",
            "A pause has to be more than zero, but `(0.3s - 0.1s - 0.2s)` isn't.",
            "Write a pause longer than zero, like `{w 0.5}`."
        },
        {
            "== a.b\nSlow.{speed (1 - 1)} Done.\n",
            "A speed has to be more than zero, but `(1 - 1)` isn't.",
            "Write a speed above zero, like `{speed 0.5}`, or `{speed}` to return to the player's setting."
        },
        {
            "== a.b\n@wait (-1s)\n",
            "The time to `@wait` has to be more than zero, but `(-1s)` isn't.",
            "Write a time longer than zero, like `@wait 0.5s`."
        },
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
        {
            $"{Cast}mria: Hi.\n",
            "I don't know an actor called `mria`.",
            "Did you mean `mira`? If this line is narration, escape the colon: `mria\\:`."
        },
        {
            $"{Cast}mira (smirk): Hi.\n",
            "Mira has no pose called `smirk`.",
            "Mira's poses are `neutral` and `smug`."
        },
        {
            "@actor rex\n    name: Rex\n\n== a.b\nrex (happy): Hi.\n",
            "Rex has no pose called `happy`.",
            "Give Rex poses with `poses:` under `@actor rex`."
        },
        {
            $"{Cast}mira (to Rex): Fine.\n",
            "`(to Rex)` isn't a pose: a pose is a single name.",
            "For how a line is said, use a `//` comment. If this is narration, escape the colon: `mira (to Rex)\\:`."
        },
        {
            $"{Cast}mira:Hi there. #id:k7qp2x\n",
            "There's no space after `mira:`.",
            "Add one, or escape the colon if this is narration: `mira\\:Hi there.`."
        },
        {
            $"{Cast}mira:\n",
            "`mira:` has nothing after it.",
            "Write what Mira says after the colon, or write `mira: {w}` for a box with only their name. To change their pose without a line, write `mira (pose):`."
        },
        {
            "@enum position: left, right\n@command show(at: position)\n\n== a.b\n@show att=left\n",
            "`@show` has no parameter called `att`.",
            "Did you mean `at`?"
        },
        {
            "@command move(who: string, to: string)\n\n== a.b\n@move \"mira\"\n",
            "`@move` needs a value for `to`.",
            "Add it after the others, or by name: `to=…`."
        },
        {
            "@var $has_key = false\n\n== a.b\nKey {$has_key}.\n",
            "I can't show `$has_key` in text, because it's a `bool`.",
            "Show text that depends on it instead: `{if $has_key}…{else}…{/if}`."
        },
        {
            "@tag thought\n\n== a.b\nHm. #thougth\n",
            "I don't know a tag called `#thougth`.",
            "Did you mean `#thought`? If this is text, escape it: `\\#thougth`."
        },
        {
            "@enum box_style: phone, letter\n@tag box: box_style\n\n== a.b\nHi. #box\n",
            "`#box` needs a value.",
            "Write a value after the colon: `#box:phone`."
        },
        {
            "@enum box_style: phone, letter\n@tag box: box_style\n\n== a.b\nHi. #box:phon\n",
            "`phon` isn't a `box_style`, which `#box` takes.",
            "Did you mean `#box:phone`?"
        },
    };

    [Theory]
    [InlineData("@var $n = 0\n\n== a.b\n@if 0 < $n < 3\n    Hm.\n", "PIB1061")]
    [InlineData("@var $n = 0\n\n== a.b\n@if $n == 1 != true\n    Hm.\n", "PIB1061")]
    [InlineData("@function item_name(id: string) -> string\n\n== a.b\nYou found {item_name (\"key\")}.\n", "PIB1051")]
    public void Compile_SyntaxProblemInExpression_ReportsOnlyIt(string text, string code) =>
        Assert.Equal([code], Diagnose(text).Select(diagnostic => diagnostic.Code));

    [Fact]
    public void Compile_SpeakerNotDeclared_NamesSpeakerByIdInSyntaxMessage()
    {
        Diagnostic diagnostic = Assert.Single(Diagnose("== a.b\nmira:\n"), diagnostic => diagnostic.Code == "PIB1054");

        Assert.StartsWith("Write what mira says", diagnostic.Help);
    }

    private const string Cast = "@actor mira\n    name: Mira\n    poses: neutral, smug\n\n== a.b\n";

    [Theory]
    [MemberData(nameof(Messages))]
    public void Compile_Problem_ReportsMessageAndHelp(string text, string message, string? help)
    {
        Diagnostic diagnostic = Assert.Single(Diagnose(text));

        Assert.Equal((message, help), (diagnostic.Message, diagnostic.Help));
    }

    [Theory]
    [InlineData("== a.b\nWait.{w (1 / 0)}{w (79228162514264337593543950335 * 2)} Done.\n")]
    [InlineData("== a.b\nWait.{speed (1 / 0)}{speed (79228162514264337593543950335 * 2)} Done.\n")]
    public void Compile_PacingValueThatIsNotRepresentable_IsNotConstant(string text) => Assert.Empty(Diagnose(text));

    [Theory]
    [InlineData("== a.b\nSlow.{speed 0s} Done.\n")]
    [InlineData("== a.b\nSlow.{w \"0\"} Done.\n")]
    [InlineData("== a.b\n@wait \"0\"\n")]
    public void Compile_PacingValueOfWrongType_ReportsOnlyTheType(string text) =>
        Assert.Equal(["PIB2031"], Diagnose(text).Select(diagnostic => diagnostic.Code));

    [Fact]
    public void Compile_ProblemInsideBrokenExpression_ReportsOnce()
    {
        Diagnostic[] diagnostics = Diagnose("@var $n = 0\n\n== a.b\n@if ($missing + 1) * 2 > $n and not $missing\n    Hm.\n");

        Assert.Equal(["PIB2030", "PIB2030"], diagnostics.Select(diagnostic => diagnostic.Code));
    }

    /// <summary>Compiles a story of one file, leaving out style and missing line IDs, which these stories don't write.</summary>
    private static Diagnostic[] Diagnose(string text) =>
        [.. WithoutStyle.Compile(new SourceText("story.pib", text)).Diagnostics.Where(diagnostic => diagnostic.Code is not "PIB3010")];
}
