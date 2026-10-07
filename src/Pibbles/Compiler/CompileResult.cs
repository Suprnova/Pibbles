using Pibbles.Diagnostics;

namespace Pibbles.Compiler;

/// <summary>What <see cref="StoryCompiler.Compile"/> made of a set of sources.</summary>
/// <param name="Story">The compiled story, or <see langword="null"/> if the sources have errors.</param>
/// <param name="Diagnostics">
/// Every problem in the sources, after each file's <c>.editorconfig</c> severities, so a warning raised to an error
/// counts as one. A story can have diagnostics and still compile, when none of them is an error.
/// </param>
public sealed record CompileResult(Story? Story, IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Whether any diagnostic is an error. There's no <see cref="Story"/> when there is.</summary>
    public bool HasErrors => Story is null;
}
