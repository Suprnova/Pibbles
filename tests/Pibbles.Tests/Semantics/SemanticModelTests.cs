using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

public class SemanticModelTests
{
    private const string CastPath = "story/cast.pib";
    private const string KitchenPath = "story/kitchen.pib";

    private const string Cast = """
        @enum position: left, right
        @actor mira
            name: Mira
            poses: happy
        @var $has_key = false
        @command show(who: actor, at: position = left)
        @markup clue
        @icon interact
        @tag thought
        @function has_item(id: string) -> bool
        """;

    private const string Kitchen = """
        @prefix kitchen

        == .door #was:.front_door
        mira (happy): [clue]Hi[/clue] {icon interact} #thought
        @show mira at=right
        @set $has_key = true
        @if $has_key and has_item("key") and visits(.door) > 0
            @jump kitchen.door
        """;

    private readonly Compilation compilation = Compilation.Create([new(CastPath, Cast), new(KitchenPath, Kitchen)]);

    /// <summary>
    /// A position, marked with <c>|</c> inside text that appears once in its file; the symbol there, by type and name;
    /// and how many times the story uses that symbol.
    /// </summary>
    public static TheoryData<string, string, Type, string, int> Positions { get; } = new()
    {
        { KitchenPath, "|mira (happy)", typeof(ActorSymbol), "mira", 2 },
        { KitchenPath, "mira (ha|ppy)", typeof(PoseSymbol), "happy", 1 },
        { KitchenPath, "[cl|ue]", typeof(MarkupSymbol), "clue", 1 },
        { KitchenPath, "{icon int|eract}", typeof(IconSymbol), "interact", 1 },
        { KitchenPath, "#th|ought", typeof(TagSymbol), "thought", 1 },
        { KitchenPath, "@sh|ow", typeof(CommandSymbol), "show", 1 },
        { KitchenPath, "|at=right", typeof(ParameterSymbol), "at", 1 },
        { KitchenPath, "at=rig|ht", typeof(EnumMemberSymbol), "right", 1 },
        { KitchenPath, "@set $has_key|", typeof(VariableSymbol), "has_key", 2 },
        { KitchenPath, "has_i|tem(", typeof(FunctionSymbol), "has_item", 1 },
        { KitchenPath, "visits(.d|oor)", typeof(NodeSymbol), "kitchen.door", 2 },
        { KitchenPath, "@jump kitchen.d|oor", typeof(NodeSymbol), "kitchen.door", 2 },
        { KitchenPath, "== .d|oor", typeof(NodeSymbol), "kitchen.door", 2 },
        { KitchenPath, "#was:.front|_door", typeof(NodeSymbol), "kitchen.door", 2 },
        { CastPath, "@actor m|ira", typeof(ActorSymbol), "mira", 2 },
        { CastPath, "at: pos|ition", typeof(EnumSymbol), "position", 1 },
        { CastPath, "position = le|ft", typeof(EnumMemberSymbol), "left", 1 },
    };

    [Theory]
    [MemberData(nameof(Positions))]
    public void GetSymbolAt_Name_FindsSymbolAndItsUses(string path, string marked, Type type, string name, int uses)
    {
        Symbol? symbol = compilation.Model.GetSymbolAt(path, PositionOf(path, marked));

        Assert.NotNull(symbol);
        Assert.Equal((type, name, uses), (symbol.GetType(), symbol.Name, compilation.Model.FindReferences(symbol).Count));
    }

    [Fact]
    public void GetSymbolAt_Whitespace_FindsNothing() => Assert.Null(compilation.Model.GetSymbolAt(KitchenPath, PositionOf(KitchenPath, "Hi[/clue] |{icon")));

    [Fact]
    public void FindReferences_ActorUsedInAnotherFile_PointsAtEachUse()
    {
        ActorSymbol mira = compilation.Model.Symbols<ActorSymbol>().Single();

        string[] uses = [.. compilation.Model.FindReferences(mira).Select(location => $"{location.Path}:{location.Start.Line + 1}:{location.Start.Column + 1}")];

        Assert.Equal(["story/kitchen.pib:4:1", "story/kitchen.pib:5:7"], uses);
    }

    [Fact]
    public void Location_DeclaredSymbol_IsItsDeclaredName()
    {
        NodeSymbol door = compilation.Model.Symbols<NodeSymbol>().Single();

        Assert.Equal((KitchenPath, 2, 3, 8), (door.Location!.Value.Path, door.Location.Value.Start.Line, door.Location.Value.Start.Column, door.Location.Value.End.Column));
    }

    [Fact]
    public void Symbols_BuiltIn_HaveNoLocation()
    {
        MarkupSymbol[] builtIn = [.. compilation.Model.Symbols<MarkupSymbol>().Where(markup => markup.Location is null)];

        Assert.Equal(["b", "i", "u", "s", "color", "speed"], builtIn.Select(markup => markup.Name));
    }

    [Fact]
    public void Symbols_Nested_ListsPosesMembersAndParameters()
    {
        Assert.Equal(["happy"], compilation.Model.Symbols<PoseSymbol>().Select(pose => pose.Name));
        Assert.Equal(["left", "right"], compilation.Model.Symbols<EnumMemberSymbol>().Select(member => member.Name));
        Assert.Contains(compilation.Model.Symbols<ParameterSymbol>(), parameter => parameter.Name == "who");
    }

    private int PositionOf(string path, string marked)
    {
        string text = compilation.SyntaxTrees.Single(tree => tree.Source.Path == path).Source.Text;
        string needle = marked.Replace("|", "", StringComparison.Ordinal);
        int start = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0 && start == text.LastIndexOf(needle, StringComparison.Ordinal), $"`{needle}` appears once in {path}.");
        return start + marked.IndexOf('|', StringComparison.Ordinal);
    }
}
