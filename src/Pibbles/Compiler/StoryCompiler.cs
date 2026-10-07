using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Compiler;

/// <summary>Compiles a story's sources into a <see cref="Story"/>.</summary>
public static class StoryCompiler
{
    /// <summary>Compiles a story. It never throws on bad sources; every problem in them is in <see cref="CompileResult.Diagnostics"/>.</summary>
    /// <param name="sources">Every source file in the story. Each path should be unique, since diagnostics refer to files by path.</param>
    /// <param name="options">How to compile it, such as each file's diagnostic severities, or <see langword="null"/> for the defaults.</param>
    /// <returns>The story if no diagnostic is an error, and every diagnostic either way.</returns>
    /// <exception cref="ArgumentException">Two sources have the same path, which is a mistake in how the host gathered them, not in the story.</exception>
    public static CompileResult Compile(IEnumerable<SourceText> sources, CompilationOptions? options = null)
    {
        SourceText[] files = [.. sources];
        if (files.GroupBy(file => file.Path).FirstOrDefault(group => group.Count() > 1) is { } repeated)
            throw new ArgumentException($"Two sources have the path `{repeated.Key}`. Each source needs a path of its own.", nameof(sources));

        Compilation compilation = Compilation.Create(files, options);
        bool hasErrors = compilation.Diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error);
        return new(hasErrors ? null : StoryLowerer.Lower(compilation), compilation.Diagnostics);
    }
}
