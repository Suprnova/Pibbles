using Pibbles.Syntax;

namespace Pibbles.Diagnostics;

/// <summary>Where a diagnostic points: a span in a file, with its start and end already turned into lines and columns.</summary>
/// <param name="Path">The file's path, as given in its <see cref="SourceText"/>.</param>
/// <param name="Span">The marked characters.</param>
/// <param name="Start">The line and column where <paramref name="Span"/> starts.</param>
/// <param name="End">The line and column just past the end of <paramref name="Span"/>.</param>
public readonly record struct SourceLocation(string Path, TextSpan Span, LinePosition Start, LinePosition End);
