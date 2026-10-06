using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// The declarations every story has without writing them, parsed like any other file. Their names are reserved, so a
/// story can't declare them again. <c>speed</c> and <c>visits</c> are declared like the host's markup and functions, but the core handles them.
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
        @markup speed(factor: number)

        @function visits(target: node) -> number
        """;

    public static SyntaxTree Tree { get; } = SyntaxTree.Parse(new SourceText(Path, Text));
}
