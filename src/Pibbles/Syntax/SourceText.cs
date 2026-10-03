using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// One source file's text, with a line map that turns character positions into lines and columns.
/// </summary>
/// <remarks>
/// The core never reads files. Hosts pass each file in as a path and its text. A line ends at
/// <c>\r\n</c>, <c>\n</c> or <c>\r</c>.
/// </remarks>
/// <remarks>Creates a source file from its path and text.</remarks>
/// <param name="path">The file's path, as diagnostics should show it.</param>
/// <param name="text">The file's full text.</param>
public sealed class SourceText(string path, string text)
{
    private readonly int[] lineStarts = FindLineStarts(text);

    /// <summary>The file's path, as diagnostics show it.</summary>
    public string Path { get; } = path;

    /// <summary>The file's full text.</summary>
    public string Text { get; } = text;

    /// <summary>The total number of lines.</summary>
    public int LineCount => lineStarts.Length;

    /// <summary>Finds the line and column of a character position.</summary>
    /// <param name="position">A position from 0 to the length of <see cref="Text"/>, inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is outside the text.</exception>
    public LinePosition GetLinePosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Text.Length);

        int line = Array.BinarySearch(lineStarts, position);
        if (line < 0)
            line = ~line - 1;

        return new(line, position - lineStarts[line]);
    }

    /// <summary>Finds where a span starts and ends, as lines and columns.</summary>
    /// <param name="span">A span inside the text.</param>
    public SourceLocation GetLocation(TextSpan span) => new(Path, span, GetLinePosition(span.Start), GetLinePosition(span.End));

    /// <summary>Gets the span of a line's text, without its line break.</summary>
    /// <param name="line">A 0-based line number less than <see cref="LineCount"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> isn't a line in the text.</exception>
    public TextSpan GetLineSpan(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, LineCount);

        int start = lineStarts[line];
        int end = start;
        while (end < Text.Length && Text[end] is not ('\r' or '\n'))
            end++;

        return new(start, end - start);
    }

    private static int[] FindLineStarts(string text)
    {
        List<int> starts = [0];
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '\r' && i + 1 < text.Length && text[i + 1] is '\n')
                i++;

            if (text[i] is '\r' or '\n')
                starts.Add(i + 1);
        }

        return [.. starts];
    }
}
