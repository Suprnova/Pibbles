using System.Globalization;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests.Semantics;

/// <summary>Checks each kind of name against the reserved words in <c>docs/language/reference.md</c>, copied here so the test doesn't check the code against itself.</summary>
public class ReservedWordsTests
{
    private static readonly string[] Statement =
        ["prefix", "actor", "enum", "var", "command", "markup", "icon", "tag", "function", "if", "elif", "else", "set", "jump", "call", "return", "end", "wait",
         "sequence", "cycle", "once", "term", "resume", "shuffle"];

    private static readonly string[] Brace = ["w", "p", "br", "icon", "if", "elif", "else", "auto", "sequence", "cycle", "shuffle", "once"];

    private static readonly string[] Value = ["true", "false", "and", "or", "not"];

    private static readonly string[] Argument = ["wait", "nowait", "speaker"];

    private static readonly string[] BuiltInFunctions = ["visits", "random"];

    private static readonly string[] BuiltInTypes = ["bool", "number", "string", "duration", "node", "actor"];

    private static readonly string[] BuiltInMarkup = ["speed", "b", "i", "u", "s", "color"];

    private static readonly string[] ReservedTags = ["id", "was", "migrates", "draft", "voice", "unvoiced"];

    private static readonly string[] Contextual = ["name", "poses", "inline", "waits", "required", "persona"];

    private static readonly string[] Words =
        [.. Statement.Concat(Brace).Concat(Value).Concat(Argument).Concat(BuiltInFunctions).Concat(BuiltInTypes).Concat(BuiltInMarkup).Concat(ReservedTags).Concat(Contextual).Distinct()];

    public static TheoryData<string, string[]> Kinds { get; } = new()
    {
        { "@command {0}()", Statement },
        { "@markup {0}", BuiltInMarkup },
        { "@function {0}() -> bool", [.. Brace, .. Value, .. BuiltInFunctions] },
        { "@enum {0}: member", BuiltInTypes },
        { "@enum things: {0}", [.. Value, .. Argument] },
        { "== {0}", [.. Value, .. Argument] },
        { "@actor {0}\n    name: Someone", [.. Value, .. Argument, .. Brace] },
        { "@command show({0}: number)", ["wait", "nowait"] },
        { "@tag {0}", ReservedTags },
        { "@actor someone\n    poses: {0}", [] },
        { "@icon {0}", [] },
        { "@var ${0} = 1", [] },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Compile_DeclaredName_IsReservedExactlyWhereReferenceSays(string declaration, string[] reserved)
    {
        string[] rejected = [.. Words.Where(word => IsRejected(string.Format(CultureInfo.InvariantCulture, declaration, word)))];

        Assert.Equal(reserved.Distinct().Order(), rejected.Order());
    }

    /// <summary>Whether a one-line story reports a reserved name. Anything other than that, or nothing, fails the test.</summary>
    private static bool IsRejected(string text) => Compilation.Create([new SourceText("story.pib", text)]).Diagnostics switch
    {
        [] => false,
        [{ Code: "PIB2062" }] => true,
        var diagnostics => throw new Xunit.Sdk.XunitException($"`{text}` reported {string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Code))}."),
    };
}
