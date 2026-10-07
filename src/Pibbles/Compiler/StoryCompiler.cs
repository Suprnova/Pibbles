using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Compiler;

/// <summary>Compiles a story's sources into a <see cref="Story"/>.</summary>
public static class StoryCompiler
{
    /// <summary>Compiles a story. It never throws on bad input; every problem is in <see cref="CompileResult.Diagnostics"/>.</summary>
    /// <param name="sources">Every source file in the story. Each path should be unique, since diagnostics refer to files by path.</param>
    /// <param name="options">How to compile it, such as each file's diagnostic severities, or <see langword="null"/> for the defaults.</param>
    /// <returns>The story if no diagnostic is an error, and every diagnostic either way.</returns>
    public static CompileResult Compile(IEnumerable<SourceText> sources, CompilationOptions? options = null)
    {
        Compilation compilation = Compilation.Create(sources, options);
        bool hasErrors = compilation.Diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error);
        return new(hasErrors ? null : StoryLowerer.Lower(compilation), compilation.Diagnostics);
    }
}
