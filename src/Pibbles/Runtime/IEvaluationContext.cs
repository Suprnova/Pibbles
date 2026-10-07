using Pibbles.Compiler;

namespace Pibbles.Runtime;

/// <summary>What expressions need from a running story: its variables, its visit counts and its host functions.</summary>
internal interface IEvaluationContext
{
    /// <summary>The current value of a variable.</summary>
    Value GetVariable(Semantics.VariableSymbol variable);

    /// <summary>How many times the node with this current name has been visited.</summary>
    int GetVisits(string node);

    /// <summary>Calls a host function with its arguments, which are in parameter order.</summary>
    Value CallFunction(CallExpr call, IReadOnlyList<Value> arguments);

    /// <summary>Receives a problem the story carried on from.</summary>
    void Warn(RuntimeWarning warning);
}
