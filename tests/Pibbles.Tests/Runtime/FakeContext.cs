using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Semantics;

namespace Pibbles.Tests.Runtime;

/// <summary>An evaluation context with variables, visit counts and host functions set by the test, which collects the warnings.</summary>
internal sealed class FakeContext : IEvaluationContext
{
    private readonly Story? story;
    private readonly HostFunctions functions;

    public FakeContext(Story? story = null, HostFunctions? functions = null)
    {
        this.story = story;
        this.functions = functions ?? new HostFunctions()
            .Add("ping", () =>
            {
                Pings++;
                return true;
            })
            .Add("twice", (decimal n) => n * 2)
            .Add("pause", (TimeSpan d) => d);
    }

    public Dictionary<VariableSymbol, Value> Variables { get; } = [];

    public Dictionary<string, int> Visits { get; } = [];

    public List<RuntimeWarning> Warnings { get; } = [];

    public int Pings { get; private set; }

    public Value GetVariable(VariableSymbol variable) => Variables[variable];

    public int GetVisits(string node) => Visits.GetValueOrDefault(node);

    public Value CallFunction(CallExpr call, IReadOnlyList<Value> arguments) =>
        functions.Invoke(story ?? throw new InvalidOperationException("This context has no story."), call, arguments, Warnings.Add);

    public void Warn(RuntimeWarning warning) => Warnings.Add(warning);
}
