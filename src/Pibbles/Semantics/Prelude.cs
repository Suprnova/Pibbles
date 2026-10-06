using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// The declarations every story has without writing them, parsed like any other file. Their names are reserved, so a
/// story can't declare them again. <c>visits</c> is declared like a host function, but the runtime answers it.
/// </summary>
internal static class Prelude
{
    public const string Path = "<prelude>";

    private const string Text = """
        @markup b
        @markup i
        @markup u
        @markup s
        @markup color(value: string)

        @function visits(target: node) -> number
        """;

    public static SyntaxTree Tree { get; } = SyntaxTree.Parse(new SourceText(Path, Text));
}
