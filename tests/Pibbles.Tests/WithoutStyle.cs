using Pibbles.Configuration;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Tests;

/// <summary>
/// Compiles stories for tests about something other than style, with the style rules off, the way a project turns them
/// off in <c>.editorconfig</c>.
/// </summary>
internal static class WithoutStyle
{
    private static readonly FileSettings Settings = FileSettings.From([new("pibbles_diagnostic.category-style.severity", "none")]);

    public static Compilation Compile(params SourceText[] sources) =>
        Compilation.Create(sources, new(sources.ToDictionary(source => source.Path, _ => Settings)));
}
