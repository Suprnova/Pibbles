using Pibbles.Configuration;

namespace Pibbles.Semantics;

/// <summary>How a story is compiled.</summary>
/// <param name="Settings">
/// Each source file's settings from <c>.editorconfig</c>, by its path as given in its <see cref="Syntax.SourceText"/>. A
/// file that isn't listed uses <see cref="FileSettings.None"/>.
/// </param>
public sealed record CompilationOptions(IReadOnlyDictionary<string, FileSettings> Settings)
{
    /// <summary>No settings for any file.</summary>
    public static CompilationOptions Default { get; } = new(new Dictionary<string, FileSettings>());
}
