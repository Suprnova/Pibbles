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
    private IReadOnlyList<NodeInfo>? nodeInfos;

    internal Story(
        IReadOnlyDictionary<string, CompiledNode> nodes,
        IReadOnlyDictionary<string, string> aliases,
        IReadOnlyList<StoryVariable> variables,
        IReadOnlyDictionary<string, Template> templates,
        IReadOnlyDictionary<string, IdSite> sites,
        IReadOnlySet<string> fallbackIds,
        IReadOnlyDictionary<string, ActorSymbol> actors,
        IReadOnlyDictionary<string, TagSymbol> tags,
        IReadOnlyDictionary<string, EnumSymbol> enums,
        IReadOnlyDictionary<string, FunctionSymbol> functions)
    {
        CompiledNodes = nodes;
        Aliases = aliases;
        Variables = variables;
        Templates = templates;
        Sites = sites;
        FallbackIds = fallbackIds;
        Actors = actors;
        Tags = tags;
        Enums = enums;
        Functions = functions;
    }

    /// <summary>Every node: its current name, and the old names from its <c>#was:</c> tags that the host can also start it by.</summary>
    public IReadOnlyList<NodeInfo> Nodes => nodeInfos ??= [.. CompiledNodes.Keys.Select(name => new NodeInfo(name, [.. Aliases.Where(alias => alias.Value == name).Select(alias => alias.Key)]))];

    /// <summary>Every node's code, by its current name.</summary>
    internal IReadOnlyDictionary<string, CompiledNode> CompiledNodes { get; }

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

    /// <summary>The tags the story declares, by name.</summary>
    internal IReadOnlyDictionary<string, TagSymbol> Tags { get; }

    /// <summary>The enums the story declares, by name.</summary>
    internal IReadOnlyDictionary<string, EnumSymbol> Enums { get; }

    /// <summary>
    /// Checks that the host's enum has exactly the members of one of the story's enums, so a renamed member fails when the
    /// game starts instead of in the middle of a scene. It never throws for a mismatch.
    /// </summary>
    /// <typeparam name="TEnum">The host's enum.</typeparam>
    /// <param name="storyEnum">The name of the enum in the story.</param>
    /// <returns>The mismatches, or an empty list if the members are the same.</returns>
    public IReadOnlyList<HostEnumProblem> ValidateEnum<TEnum>(string storyEnum)
        where TEnum : struct, Enum
    {
        if (!Enums.TryGetValue(storyEnum, out EnumSymbol? declared))
            return [new(HostEnumProblemKind.UnknownEnum, storyEnum, $"The story doesn't declare an enum called `{storyEnum}`.")];

        string[] host = Enum.GetNames<TEnum>();
        string[] members = [.. declared.Members.Select(member => member.Name)];
        return
        [
            .. members.Except(host).Select(member => new HostEnumProblem(HostEnumProblemKind.MissingMember, member, $"`{storyEnum}` has `{member}` in the story, but `{typeof(TEnum).Name}` has no member with that name.")),
            .. host.Except(members).Select(member => new HostEnumProblem(HostEnumProblemKind.ExtraMember, member, $"`{typeof(TEnum).Name}` has `{member}`, but `{storyEnum}` in the story doesn't.")),
        ];
    }
}

/// <summary>A node of the story, as a host sees it.</summary>
/// <param name="Name">The node's current name.</param>
/// <param name="Aliases">The node's old names, from its <c>#was:</c> tags, which still start it.</param>
public sealed record NodeInfo(string Name, IReadOnlyList<string> Aliases);

/// <summary>A mismatch between the host's enum and the story's, found by <see cref="Story.ValidateEnum{TEnum}"/>.</summary>
/// <param name="Kind">What kind of mismatch.</param>
/// <param name="Member">The member (or, for an unknown enum, the enum) the problem is about.</param>
/// <param name="Message">What's wrong, written for the game's developers.</param>
public sealed record HostEnumProblem(HostEnumProblemKind Kind, string Member, string Message);

/// <summary>The kinds of mismatch <see cref="Story.ValidateEnum{TEnum}"/> finds. New kinds can be added, so keep a default arm.</summary>
public enum HostEnumProblemKind
{
    /// <summary>The story has no enum with that name.</summary>
    UnknownEnum,

    /// <summary>The story's enum has a member the host's doesn't.</summary>
    MissingMember,

    /// <summary>The host's enum has a member the story's doesn't.</summary>
    ExtraMember,
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
