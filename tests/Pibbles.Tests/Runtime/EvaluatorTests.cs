using System.Globalization;
using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Runtime;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Runtime;

public class EvaluatorTests
{
    private const string Defs = """
        @enum position: left, right
        @actor mira
            name: Mira
        @var $n = 3
        @var $d = 2s
        @var $s = "Sam"
        @var $b = true
        @var $e: position = left
        @var $out_number = 0
        @var $out_duration = 0s
        @var $out_string = ""
        @var $out_bool = false
        @var $out_position: position = left
        @var $out_actor: actor = mira
        @var $out_node: node = t.n
        @function ping() -> bool
        @function twice(n: number) -> number
        @function pause(d: duration) -> duration

        == t.n
        @set $out_number = 0

        == t.m
        """;

    private static readonly int ExpressionLine = Defs.Split('\n').Length + 1;

    public static TheoryData<string, string, string> Operations { get; } = new()
    {
        { "bool", "not true", "false" },
        { "bool", "true and false", "false" },
        { "bool", "false or true", "true" },
        { "bool", "$n == 3", "true" },
        { "bool", "$n == 3.00", "true" },
        { "bool", "1.50 == 1.5", "true" },
        { "bool", "$n != 3", "false" },
        { "bool", "$s == \"Sam\"", "true" },
        { "bool", "$s != \"sam\"", "true" },
        { "bool", "$s == \"sam\"", "false" },
        { "bool", "$b == true", "true" },
        { "bool", "$e == left", "true" },
        { "bool", "$e != right", "true" },
        { "bool", "$e == right", "false" },
        { "bool", "$d == 2s", "true" },
        { "bool", "$d == 2", "true" },
        { "bool", "$d == 2.0s", "true" },
        { "bool", "$n < 5", "true" },
        { "bool", "$n <= 3", "true" },
        { "bool", "$n > 3", "false" },
        { "bool", "$n >= 3", "true" },
        { "bool", "$d < 3s", "true" },
        { "bool", "$d > 1", "true" },
        { "bool", "$d >= 2s", "true" },
        { "bool", "$d <= 1", "false" },
        { "bool", "visits(t.n) > 1", "true" },
        { "number", "$n + 2", "5" },
        { "number", "0.1 + 0.2", "0.3" },
        { "number", "$n - 5", "-2" },
        { "number", "$n * 4", "12" },
        { "number", "7 / 2", "3.5" },
        { "number", "$d / 1s", "2" },
        { "number", "$d / (0.5s + 0.5s)", "2" },
        { "number", "1 / 3 * 3", "0.9999999999999999999999999999" },
        { "number", "7 % 3", "1" },
        { "number", "-7 % 3", "2" },
        { "number", "7 % -3", "-2" },
        { "number", "-7 % -3", "-1" },
        { "number", "6 % 3", "0" },
        { "number", "-$n", "-3" },
        { "number", "visits(t.n) + 1", "5" },
        { "number", "twice($n)", "6" },
        { "duration", "$d + 1s", "3s" },
        { "duration", "$d + 1", "3s" },
        { "duration", "0.5s + 1", "1.5s" },
        { "duration", "0.5s + (1 + 2)", "3.5s" },
        { "duration", "$d - 0.5s", "1.5s" },
        { "duration", "$d * 2", "4s" },
        { "duration", "2 * $d", "4s" },
        { "duration", "$d / 4", "0.5s" },
        { "duration", "-$d", "-2s" },
        { "duration", "pause(1)", "1s" },
        { "duration", "pause($d)", "2s" },
        { "string", "$s + \"!\"", "\"Sam!\"" },
        { "string", "$s + $s", "\"SamSam\"" },
        { "position", "$e", "left" },
        { "position", "right", "right" },
        { "actor", "mira", "mira" },
        { "node", "t.m", "t.m" },
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public void Evaluate_LoweredExpression_GivesTheReferenceResult(string type, string expression, string expected)
    {
        Evaluation result = Run(type, expression);

        Assert.Equal(expected, result.Shown);
        Assert.Empty(result.Warnings);
    }

    private static readonly (string Type, string Expression, string Expected, RuntimeWarningKind Kind)[] WarnedCases =
    [
        ("number", "$n / 0", "0", RuntimeWarningKind.DivisionByZero),
        ("number", "$n % 0", "0", RuntimeWarningKind.DivisionByZero),
        ("number", "$n / (3 - 3)", "0", RuntimeWarningKind.DivisionByZero),
        ("duration", "$d / 0", "0s", RuntimeWarningKind.DivisionByZero),
        ("number", "$d / 0s", "0", RuntimeWarningKind.DivisionByZero),
        ("number", "79228162514264337593543950335 + 1", "79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "-79228162514264337593543950335 - 1", "-79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "79228162514264337593543950335 - -1", "79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "79228162514264337593543950335 * 2", "79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "79228162514264337593543950335 * -2", "-79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "-79228162514264337593543950335 * -2", "79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "79228162514264337593543950335 / 0.5", "79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("number", "-79228162514264337593543950335 / 0.5", "-79228162514264337593543950335", RuntimeWarningKind.Overflow),
        ("duration", "79228162514264337593543950335s * 2", "79228162514264337593543950335s", RuntimeWarningKind.Overflow),
    ];

    public static TheoryData<string, string, string, RuntimeWarningKind> Warned { get; } = [.. WarnedCases.Select(row => (row.Type, row.Expression, row.Expected, row.Kind))];

    [Theory]
    [MemberData(nameof(Warned))]
    public void Evaluate_DivisionByZeroOrOverflow_CarriesOnWithAWarningAtTheExpression(string type, string expression, string expected, RuntimeWarningKind kind)
    {
        Evaluation result = Run(type, expression);

        Assert.Equal(expected, result.Shown);
        RuntimeWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(kind, warning.Kind);
        Assert.Equal(("story.pib", ExpressionLine), (warning.Location.Path, warning.Location.Start.Line + 1));
        Assert.NotEmpty(warning.Message);
    }

    [Fact]
    public void Evaluate_ClampedOverflow_HasTheSignOfTheTrueResult()
    {
        Assert.Equal("-79228162514264337593543950335", Run("number", "79228162514264337593543950335 * -2").Shown);
        Assert.Equal("79228162514264337593543950335", Run("number", "-79228162514264337593543950335 * -2").Shown);
    }

    [Fact]
    public void Evaluate_AndOr_ShortCircuitLeftToRight()
    {
        Assert.Equal(0, Run("bool", "false and ping()").Pings);
        Assert.Equal(0, Run("bool", "true or ping()").Pings);
        Assert.Equal(1, Run("bool", "true and ping()").Pings);
        Assert.Equal(1, Run("bool", "false or ping()").Pings);
    }

    [Fact]
    public void Evaluate_WarningOnTheRightOfAFalseAnd_DoesNotHappen()
    {
        Evaluation result = Run("bool", "false and $n / 0 > 1");

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Evaluate_Expression_ChangesNothing()
    {
        (Story story, FakeContext context) = Setup();
        var set = (SetInstruction)story.Nodes["t.n"].Instructions[0];
        Value before = context.Variables[set.Variable];

        Evaluator.Evaluate(new BinaryExpr(BinaryOperator.Divide, new NumberExpr(1), new NumberExpr(0), TypeSymbol.Number, default), context);

        Assert.Equal(before, context.Variables[set.Variable]);
    }

    [Fact]
    public void Evaluate_HandBuiltRemainder_ReportsTheLocationItWasGiven()
    {
        var context = new FakeContext();
        SourceLocation location = new("here.pib", new(0, 1), new(4, 2), new(4, 3));

        Value result = Evaluator.Evaluate(new BinaryExpr(BinaryOperator.Remainder, new NumberExpr(5), new NumberExpr(0), TypeSymbol.Number, location), context);

        Assert.Equal(Value.Number(0), result);
        Assert.Equal(location, Assert.Single(context.Warnings).Location);
    }

    [Fact]
    public void RuntimeWarningKind_EveryKind_IsTriggeredByATest()
    {
        HashSet<RuntimeWarningKind> triggered = [.. WarnedCases.Select(row => row.Kind)];
        triggered.UnionWith(HostFunctionsTests.ClampedDurationWarnings().Select(warning => warning.Kind));

        Assert.Empty(Enum.GetValues<RuntimeWarningKind>().Except(triggered));
    }

    /// <summary>Compiles a story that stores <paramref name="expression"/> in a variable, and evaluates what it stores.</summary>
    private static Evaluation Run(string type, string expression)
    {
        string text = $"{Defs}\n@set $out_{type} = {expression}\n";
        (Story story, FakeContext context) = Setup(text);
        var set = (SetInstruction)story.Nodes["t.m"].Instructions[0];

        Value value = Evaluator.Evaluate(set.Value, context);
        return new(Show(value), context.Warnings, context.Pings);
    }

    private static (Story, FakeContext) Setup(string? text = null)
    {
        CompileResult result = StoryCompiler.Compile([new("story.pib", text ?? Defs)]);
        Story story = result.Story ?? throw new Xunit.Sdk.XunitException(string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")));

        var context = new FakeContext(story);
        foreach (StoryVariable variable in story.Variables)
            context.Variables[variable.Variable] = Evaluator.Evaluate(variable.StartingValue, context);

        context.Visits["t.n"] = 4;
        return (story, context);
    }

    private static string Show(Value value) =>
        value.Type == TypeSymbol.Bool ? (value.AsBool ? "true" : "false")
        : value.Type == TypeSymbol.Number ? value.AsDecimal.ToString(CultureInfo.InvariantCulture)
        : value.Type == TypeSymbol.Duration ? value.AsDecimal.ToString(CultureInfo.InvariantCulture) + "s"
        : value.Type == TypeSymbol.String ? $"\"{value.AsString}\""
        : value.Type == TypeSymbol.Node ? value.AsString
        : value.AsSymbol.Name;

    private sealed record Evaluation(string Shown, List<RuntimeWarning> Warnings, int Pings);
}
