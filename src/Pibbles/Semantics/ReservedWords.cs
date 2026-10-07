namespace Pibbles.Semantics;

/// <summary>
/// The words each kind of name can't be, from the reserved words in <c>docs/language/reference.md</c>. A word is
/// reserved only where it could be read two ways, so each kind avoids only the words that appear in its positions.
/// </summary>
internal static class ReservedWords
{
    private const string Planned = "for a planned feature";

    private static readonly Group Statement = new("after `@`",
        ["prefix", "actor", "enum", "var", "command", "markup", "icon", "tag", "function", "if", "elif", "else", "set", "jump", "call", "return", "end", "wait", "sequence", "cycle", "once"],
        ["term", "resume", "shuffle"]);

    private static readonly Group Brace = new("inside `{…}`", ["w", "p", "br", "speed", "icon", "if", "elif", "else"], ["auto", "sequence", "cycle", "shuffle", "once"]);

    private static readonly Group Value = new("in expressions", ["true", "false", "and", "or", "not"], []);

    private static readonly Group Argument = new("in a command's arguments", ["wait", "nowait"], ["speaker"]);

    private static readonly Group ParameterArgument = new("in a command's arguments", ["wait", "nowait"], []);

    private static readonly Group BuiltInFunction = new("for its own function", ["visits"], ["random"]);

    private static readonly Group BuiltInType = new("for its own type", ["bool", "number", "string", "duration", "node", "actor"], []);

    private static readonly Group BuiltInMarkup = new("for its own markup", ["b", "i", "u", "s", "color"], []);

    private static readonly Group ReservedTag = new("for its own tag", ["id", "was"], ["migrates", "draft", "voice", "unvoiced"]);

    /// <summary>
    /// Says where Pibbles uses <paramref name="name"/> if a name of <paramref name="kind"/> can't be it, such as
    /// <c>after `@`</c>, or returns <see langword="null"/> if the name is free.
    /// </summary>
    /// <param name="name">The name as written. A node name with a dot is never reserved, since only a single word can be read as a keyword.</param>
    /// <param name="kind">The kind of name being declared.</param>
    public static string? UseOf(string name, SymbolKind kind)
    {
        Group[] groups = kind switch
        {
            SymbolKind.Command => [Statement],
            SymbolKind.Markup => [BuiltInMarkup],
            SymbolKind.Function => [Brace, Value, BuiltInFunction],
            SymbolKind.Enum => [BuiltInType],
            SymbolKind.EnumMember => [Value, Argument],
            SymbolKind.Node when !name.Contains('.', StringComparison.Ordinal) => [Value, Argument],
            SymbolKind.Actor => [Value, Argument, Brace],
            SymbolKind.Parameter => [ParameterArgument],
            SymbolKind.Tag => [ReservedTag],
            _ => [],
        };

        return groups.Select(group => group.UseOf(name)).FirstOrDefault(use => use is not null);
    }

    private sealed record Group(string Use, string[] Words, string[] PlannedWords)
    {
        public string? UseOf(string name) => Words.Contains(name) ? Use : PlannedWords.Contains(name) ? Planned : null;
    }
}
