using Pibbles.Semantics;

namespace Pibbles.Compiler;

/// <summary>A compiled story, ready to run.</summary>
/// <remarks>Get one from <see cref="StoryCompiler.Compile"/>, which only returns a story when the sources have no errors.</remarks>
public sealed class Story
{
    internal Story(Compilation compilation) => Compilation = compilation;

    internal Compilation Compilation { get; }
}
