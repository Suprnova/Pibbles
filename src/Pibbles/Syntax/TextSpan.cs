namespace Pibbles.Syntax;

/// <summary>A range of characters in a source file, counted in UTF-16 code units from the start of the file.</summary>
/// <param name="Start">The index of the first character in the span.</param>
/// <param name="Length">The number of characters in the span. A zero-length span marks a position between two characters.</param>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>The index just past the last character in the span.</summary>
    public int End => Start + Length;
}
