using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Compiler;

/// <summary>A compiled story, ready to run.</summary>
/// <remarks>
/// Get one from <see cref="StoryCompiler.Compile"/>, which only returns a story when the sources have no errors. A story
/// holds everything it needs to run, and nothing from the sources: the compilation it came from is not kept.
/// </remarks>
public sealed class Story
{
    internal Story(
        IReadOnlyDictionary<string, CompiledNode> nodes,
        IReadOnlyDictionary<string, string> aliases,
        IReadOnlyList<StoryVariable> variables,
        IReadOnlyDictionary<string, Template> templates,
        IReadOnlyDictionary<string, IdSite> sites,
        IReadOnlySet<string> fallbackIds,
        IReadOnlyDictionary<string, ActorSymbol> actors,
        IReadOnlyDictionary<string, FunctionSymbol> functions)
    {
        Nodes = nodes;
        Aliases = aliases;
        Variables = variables;
        Templates = templates;
        Sites = sites;
        FallbackIds = fallbackIds;
        Actors = actors;
        Functions = functions;
    }

    /// <summary>Every node, by its current name.</summary>
    internal IReadOnlyDictionary<string, CompiledNode> Nodes { get; }

    /// <summary>Each old name from a <c>#was:</c> tag, to the node's current name. A host can start a node by either.</summary>
    internal IReadOnlyDictionary<string, string> Aliases { get; }

    /// <summary>Every variable, with its starting value as an expression.</summary>
    internal IReadOnlyList<StoryVariable> Variables { get; }

    /// <summary>Every line's and option's template, by ID.</summary>
    internal IReadOnlyDictionary<string, Template> Templates { get; }

    /// <summary>Where each ID lives: a line, an option, an <c>@call</c> or a variation block.</summary>
    internal IReadOnlyDictionary<string, IdSite> Sites { get; }

    /// <summary>
    /// The IDs the compiler made up for lines, options, calls and blocks that don't write an <c>#id</c>. They're
    /// <c>~path:line</c>, which no written ID can be, and they don't survive an edit to the file.
    /// </summary>
    internal IReadOnlySet<string> FallbackIds { get; }

    /// <summary>Every declared actor, by ID, for checking what a host function returns.</summary>
    internal IReadOnlyDictionary<string, ActorSymbol> Actors { get; }

    /// <summary>The functions the story declares, which the host provides. <c>visits</c> isn't among them.</summary>
    internal IReadOnlyDictionary<string, FunctionSymbol> Functions { get; }
}

/// <summary>A node, compiled.</summary>
/// <param name="Name">The node's current name.</param>
/// <param name="Instructions">What the node does, ending in an implicit <see cref="ReturnInstruction"/>.</param>
/// <param name="Location">Where the node's header names it, for a warning about the node as a whole.</param>
internal sealed record CompiledNode(string Name, IReadOnlyList<Instruction> Instructions, SourceLocation Location);

/// <summary>A variable and where it starts.</summary>
internal sealed record StoryVariable(VariableSymbol Variable, Expr StartingValue);

/// <summary>Where an ID lives: the instruction at <paramref name="Index"/> in <paramref name="Node"/>, which for an option is its choice.</summary>
internal sealed record IdSite(string Node, int Index);
