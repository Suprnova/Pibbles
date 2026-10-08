using System.Text;
using System.Text.RegularExpressions;
using Pibbles.Diagnostics;

namespace Pibbles.Cli.Output;

/// <summary>
/// Colors a transcript's lines for a terminal, without changing a character of them, so what a writer sees in
/// <c>pibbles play</c> is the transcript it records: directives blue, answers green, speakers bold, markers cyan, stage
/// steps magenta, warnings yellow, and IDs dim.
/// </summary>
internal static partial class TranscriptColors
{
    private const string Cyan = "36";
    private const string Magenta = "35";
    private const string Dim = "2";

    /// <summary>One line of a transcript, colored.</summary>
    public static string Paint(string line)
    {
        if (line.StartsWith("start ", StringComparison.Ordinal) || line.StartsWith("set ", StringComparison.Ordinal) || line.StartsWith("stub ", StringComparison.Ordinal))
            return Ansi.Paint(line, Ansi.Blue, true);

        if (line.StartsWith("> ", StringComparison.Ordinal))
            return Ansi.Paint(line, Ansi.Green, true);

        if (line.StartsWith("  warning ", StringComparison.Ordinal))
            return Ansi.Paint(line, Ansi.Tint(DiagnosticSeverity.Warning), true);

        if (StageStep().IsMatch(line))
            return Ansi.Paint(line, Magenta, true);

        string painted = Id().Replace(Marker().Replace(line, match => Ansi.Paint(match.Value, Cyan, true)), match => Ansi.Paint(match.Value, Dim, true));
        return Speaker().Replace(painted, match => match.Groups[1].Value + Ansi.Paint(match.Groups[2].Value, Ansi.Bold, true), 1);
    }

    /// <summary>A command, pose, wait, end or variable change.</summary>
    [GeneratedRegex(@"^  (@|pose |wait |end$|\$|choice$)")]
    private static partial Regex StageStep();

    [GeneratedRegex("⟨[^⟩]*⟩")]
    private static partial Regex Marker();

    /// <summary>The ID after a line's text, before an option's state.</summary>
    [GeneratedRegex(@"(?<=  )#(~|[a-z][a-z0-9_]*)(?=( \((unavailable|chosen)\))*$)")]
    private static partial Regex Id();

    [GeneratedRegex(@"^(  )([^\s:]+)(?=: )")]
    private static partial Regex Speaker();
}

/// <summary>Writes complete lines through <see cref="TranscriptColors.Paint"/>, holding back a line until it ends.</summary>
internal sealed class ColoredLines(TextWriter inner) : TextWriter
{
    private readonly StringBuilder line = new();

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        if (value != '\n')
        {
            line.Append(value);
            return;
        }

        inner.Write(TranscriptColors.Paint(line.ToString()));
        inner.Write('\n');
        inner.Flush();
        line.Clear();
    }

    public override void Write(string? value)
    {
        foreach (char character in value ?? "")
            Write(character);
    }
}

/// <summary>Writes to two writers at once.</summary>
internal sealed class TeeWriter(TextWriter first, TextWriter second) : TextWriter
{
    public override Encoding Encoding => first.Encoding;

    public override void Write(char value)
    {
        first.Write(value);
        second.Write(value);
    }

    public override void Write(string? value)
    {
        first.Write(value);
        second.Write(value);
    }

    public override void Flush()
    {
        first.Flush();
        second.Flush();
    }
}
