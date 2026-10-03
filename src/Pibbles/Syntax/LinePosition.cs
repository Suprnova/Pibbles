namespace Pibbles.Syntax;

/// <summary>A position in a source file as a line and a column, both 0-based.</summary>
/// <remarks>Tools that show positions to people add 1 to both.</remarks>
/// <param name="Line">The 0-based line number.</param>
/// <param name="Column">The 0-based column, counted in UTF-16 code units.</param>
public readonly record struct LinePosition(int Line, int Column);
