using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Runtime;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Runtime;

public class HostFunctionsTests
{
    private const string Declarations = """
        @enum position: left, right
        @actor mira
            name: Mira
        @function a0() -> number
        @function a1(a: number) -> number
        @function a2(a: number, b: number) -> number
        @function a3(a: number, b: number, c: number) -> number
        @function a4(a: number, b: number, c: number, d: number) -> number
        @function flag(b: bool) -> bool
        @function echo(s: string) -> string
        @function lasting(d: duration) -> duration
        @function describe(p: position, a: actor, n: node) -> string
        @function pick() -> position
        @function who() -> actor
        @function where() -> node
        @function has_item(id: string) -> bool

        == t.new #was:t.old
        @end
        """;

    private static readonly string[] ArityNames = ["a0", "a1", "a2", "a3", "a4"];

    private static readonly Story Story = Compile(Declarations);

    private static readonly SourceLocation Here = new("story.pib", new(0, 1), new(6, 0), new(6, 1));

    [Fact]
    public void Add_EveryArity_CallsTheDelegateWithItsArguments()
    {
        HostFunctions functions = new HostFunctions()
            .Add("a0", () => 10m)
            .Add("a1", (decimal a) => a)
            .Add("a2", (decimal a, decimal b) => a + b)
            .Add("a3", (decimal a, decimal b, decimal c) => a + b + c)
            .Add("a4", (decimal a, decimal b, decimal c, decimal d) => a + b + c + d);

        decimal[] results = [.. ArityNames.Select((name, arity) => Call(functions, name, [.. Enumerable.Repeat(Value.Number(1), arity)]).AsDecimal)];

        Assert.Equal([10m, 1m, 2m, 3m, 4m], results);
    }

    [Fact]
    public void Invoke_EveryMappedType_ConvertsBothWays()
    {
        HostFunctions functions = new HostFunctions()
            .Add("flag", (bool b) => !b)
            .Add("echo", (string s) => s + "!")
            .Add("lasting", (TimeSpan d) => d + TimeSpan.FromSeconds(1))
            .Add("describe", (string p, string a, string n) => $"{p}/{a}/{n}");
        ActorSymbol mira = Story.Actors["mira"];
        EnumSymbol position = Story.Functions["pick"].ReturnType as EnumSymbol ?? throw new InvalidOperationException();

        Assert.True(Call(functions, "flag", Value.Bool(false)).AsBool);
        Assert.Equal("a!", Call(functions, "echo", Value.String("a")).AsString);
        Assert.Equal(1.5m, Call(functions, "lasting", Value.Duration(0.5m)).AsDecimal);
        Assert.Equal("left/mira/t.new", Call(functions, "describe", Value.Member(position.Members[0], position), Value.Actor(mira), Value.Node("t.new")).AsString);
    }

    [Theory]
    [InlineData("0.00000005", 1)]
    [InlineData("0.00000004", 0)]
    [InlineData("-0.00000005", -1)]
    [InlineData("0.5", 5_000_000)]
    public void Invoke_DurationArgument_RoundsToTheNearestTick(string seconds, long ticks)
    {
        TimeSpan received = default;
        HostFunctions functions = new HostFunctions().Add("lasting", (TimeSpan d) =>
        {
            received = d;
            return d;
        });

        Call(functions, "lasting", Value.Duration(decimal.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(ticks, received.Ticks);
    }

    [Fact]
    public void Invoke_DurationReturned_BecomesExactSeconds()
    {
        HostFunctions functions = new HostFunctions().Add("lasting", (TimeSpan _) => TimeSpan.FromTicks(15));

        Assert.Equal(0.0000015m, Call(functions, "lasting", Value.Duration(1)).AsDecimal);
    }

    [Theory]
    [InlineData("1000000000000", true)]
    [InlineData("-1000000000000", false)]
    public void Invoke_DurationBeyondTimeSpan_ClampsWithAnOverflowWarning(string seconds, bool positive)
    {
        TimeSpan received = default;
        HostFunctions functions = new HostFunctions().Add("lasting", (TimeSpan d) =>
        {
            received = d;
            return d;
        });
        List<RuntimeWarning> warnings = [];

        functions.Invoke(Story, new CallExpr(Story.Functions["lasting"], [], Here), [Value.Duration(decimal.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture))], warnings.Add);

        Assert.Equal(positive ? TimeSpan.MaxValue : TimeSpan.MinValue, received);
        RuntimeWarning warning = Assert.Single(warnings);
        Assert.Equal((RuntimeWarningKind.Overflow, Here), (warning.Kind, warning.Location));
    }

    /// <summary>Warnings from a duration too long for a <see cref="TimeSpan"/>, for the test that every warning kind is triggered.</summary>
    internal static IReadOnlyList<RuntimeWarning> ClampedDurationWarnings()
    {
        List<RuntimeWarning> warnings = [];
        new HostFunctions()
            .Add("lasting", (TimeSpan d) => d)
            .Invoke(Story, new CallExpr(Story.Functions["lasting"], [], Here), [Value.Duration(1_000_000_000_000m)], warnings.Add);
        return warnings;
    }

    [Fact]
    public void Invoke_ReturnedEnumActorAndNodeStrings_AreChecked()
    {
        string result = "left";
        HostFunctions functions = new HostFunctions()
            .Add("pick", () => result)
            .Add("who", () => result)
            .Add("where", () => result);

        result = "right";
        Assert.Equal("right", Call(functions, "pick").AsSymbol.Name);
        result = "mira";
        Assert.Equal("mira", Call(functions, "who").AsSymbol.Name);
        result = "t.new";
        Assert.Equal("t.new", Call(functions, "where").AsString);
        result = "t.old";
        Assert.Equal("t.new", Call(functions, "where").AsString);
    }

    [Theory]
    [InlineData("pick", "up")]
    [InlineData("pick", "LEFT")]
    [InlineData("who", "zed")]
    [InlineData("where", "t.nowhere")]
    [InlineData("pick", null)]
    public void Invoke_ReturnedStringThatNamesNothing_IsAHostError(string function, string? returned)
    {
        HostFunctions functions = new HostFunctions().Add("pick", () => returned!).Add("who", () => returned!).Add("where", () => returned!);

        var exception = Assert.Throws<HostFunctionException>(() => Call(functions, function));

        Assert.Equal(function, exception.Function);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void Add_Misuse_ThrowsArgumentException()
    {
        HostFunctions functions = new HostFunctions().Add("a0", () => 1m);

        Assert.ThrowsAny<ArgumentException>(() => functions.Add("a0", () => 2m));
        Assert.ThrowsAny<ArgumentException>(() => functions.Add("a1", (Func<decimal, decimal>)null!));
        Assert.ThrowsAny<ArgumentException>(() => functions.Add("visits", (string _) => 1m));
        Assert.ThrowsAny<ArgumentException>(() => functions.Add("", () => 1m));
    }

    [Fact]
    public void Add_TypeThatIsNotMapped_ThrowsArgumentException()
    {
        var functions = new HostFunctions();

        Assert.ThrowsAny<ArgumentException>(() => functions.Add("a1", (int a) => a));
        Assert.ThrowsAny<ArgumentException>(() => functions.Add("a1", (double a) => a));
        Assert.ThrowsAny<ArgumentException>(() => functions.Add("a1", (decimal a) => 1f));
        Assert.ThrowsAny<ArgumentException>(() => functions.Add("a1", (DayOfWeek a) => a));
    }

    [Fact]
    public void Validate_EverythingMatches_ReportsNothing()
    {
        HostFunctions functions = new HostFunctions()
            .Add("a0", () => 1m).Add("a1", (decimal a) => a).Add("a2", (decimal a, decimal b) => a)
            .Add("a3", (decimal a, decimal b, decimal c) => a).Add("a4", (decimal a, decimal b, decimal c, decimal d) => a)
            .Add("flag", (bool b) => b).Add("echo", (string s) => s).Add("lasting", (TimeSpan d) => d)
            .Add("describe", (string p, string a, string n) => p).Add("pick", () => "left").Add("who", () => "mira")
            .Add("where", () => "t.new").Add("has_item", (string id) => true);

        Assert.Empty(functions.Validate(Story));
    }

    [Fact]
    public void Validate_MissingMismatchedAndUndeclared_ReportsEachWithoutThrowing()
    {
        HostFunctions functions = new HostFunctions()
            .Add("has_item", (decimal id) => true)
            .Add("has_itme", (string id) => true);

        IReadOnlyList<HostFunctionProblem> problems = functions.Validate(Story);

        HostFunctionProblem mismatch = Assert.Single(problems, problem => problem.Kind is HostFunctionProblemKind.WrongSignature);
        Assert.Equal("In the story, `has_item` takes a `string` and returns a `bool`, but the registered function takes a `decimal` and returns a `bool`.", mismatch.Message);
        Assert.Equal(12, problems.Count(problem => problem.Kind is HostFunctionProblemKind.Missing));
        Assert.Equal("The story declares `a2`, which takes a `decimal` and a `decimal` and returns a `decimal`, but no function with that name is registered.", problems.First(problem => problem.Function is "a2").Message);
        HostFunctionProblem typo = Assert.Single(problems, problem => problem.Kind is HostFunctionProblemKind.NotDeclared);
        Assert.Equal("has_itme", typo.Function);
    }

    [Fact]
    public void Validate_KitchenStoryWithItsFunction_ReportsNothing()
    {
        SourceText[] sources =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "samples", "kitchen"), "*.pib", SearchOption.AllDirectories)
                .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path), File.ReadAllText(path))),
        ];
        Story kitchen = Compile(sources);

        Assert.Empty(new HostFunctions().Add("has_item", (string id) => id == "crowbar").Validate(kitchen));
        Assert.Single(new HostFunctions().Validate(kitchen).Select(problem => problem.Function), "has_item");
    }

    [Fact]
    public void Invoke_FunctionThatWasNeverRegistered_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => Call(new HostFunctions(), "a0"));
    }

    [Fact]
    public void Invoke_FunctionThatThrows_IsWrappedWithItsNameAndLocation()
    {
        var failure = new TimeoutException("the shop is closed");
        HostFunctions functions = new HostFunctions().Add("a0", () => ThrowFrom(failure));

        var exception = Assert.Throws<HostFunctionException>(() => Call(functions, "a0"));

        Assert.Equal(("a0", Here), (exception.Function, exception.Location));
        Assert.Same(failure, exception.InnerException);
        Assert.Contains("the shop is closed", exception.Message);
        Assert.Contains("story.pib line 7", exception.Message);
    }

    private static decimal ThrowFrom(Exception exception) => throw exception;

    private static Value Call(HostFunctions functions, string name, params Value[] arguments) =>
        functions.Invoke(Story, new CallExpr(Story.Functions[name], [], Here), arguments, _ => { });

    private static Story Compile(string text) => Compile([new SourceText("story.pib", text)]);

    private static Story Compile(SourceText[] sources)
    {
        CompileResult result = StoryCompiler.Compile(sources);
        return result.Story ?? throw new Xunit.Sdk.XunitException(string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")));
    }
}
