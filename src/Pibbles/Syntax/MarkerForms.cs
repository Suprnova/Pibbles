using System.Buffers;
using System.Text;
using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

/// <summary>
/// What an input method types in a marker's place: the full-width forms of ASCII punctuation and digits (U+FF01 to
/// U+FF5E), and the punctuation Japanese and Chinese input methods type for a marker key, such as <c>「</c> for <c>[</c>.
/// </summary>
/// <remarks>
/// Markers are ASCII, except in the speaker prefix. Where a form decides what a line is or what a code-mode token is, it's
/// read as its marker and reported, so the rest of the line is still checked. Elsewhere it stays text.
/// </remarks>
internal static class MarkerForms
{
    private static readonly SearchValues<char> CodeForms = SearchValues.Create(
        [.. Enumerable.Range(0xFF01, 0x5E).Select(c => (char)c).Where(c => Map(c) is not '\0'), .. "「」【】『』・、。￥《》“”"]);

    /// <summary>The ASCII marker character <paramref name="c"/> stands for, or <c>'\0'</c> if it stands for none.</summary>
    /// <param name="c">The character as written.</param>
    /// <param name="lineStart">Whether it starts a line, where <c>ー</c> is a dash. Elsewhere it's a letter, the long-vowel mark.</param>
    /// <remarks>Full-width letters and <c>＿</c> aren't markers: they're part of identifiers, which read them as they're written.</remarks>
    public static char Map(char c, bool lineStart = false) => c switch
    {
        >= '！' and <= '～' when !char.IsAsciiLetter((char)(c - 0xFEE0)) && c is not '＿' => (char)(c - 0xFEE0),
        '「' or '【' => '[',
        '」' or '】' => ']',
        '『' => '{',
        '』' => '}',
        '・' => '/',
        '、' => ',',
        '。' => '.',
        '￥' => '$',
        '《' => '<',
        '》' => '>',
        '“' or '”' => '"',
        'ー' when lineStart => '-',
        _ => '\0',
    };

    /// <summary><paramref name="text"/> with every form mapped to its marker, as the code-mode lexer reads it. Returns <paramref name="text"/> itself if it has none.</summary>
    /// <remarks>Each form is one character, as its marker is, so every position in the result is the same as in <paramref name="text"/>.</remarks>
    public static string Map(string text)
    {
        int first = text.AsSpan().IndexOfAny(CodeForms);
        if (first < 0)
            return text;

        return string.Create(text.Length, (text, first), static (chars, state) =>
        {
            state.text.CopyTo(chars);
            for (int i = state.first; i < chars.Length; i++)
            {
                if (Map(chars[i]) is not '\0' and var ascii)
                    chars[i] = ascii;
            }
        });
    }
}

/// <summary>
/// The forms read as markers in one file, reported as one <see cref="DiagnosticCatalog.InputMethodForm"/> per run of them,
/// once parsing is done. Recording a form never stops the rest of its line from being checked.
/// </summary>
internal sealed class MarkerFormLog
{

    private readonly SortedDictionary<int, (char Marker, bool Escapable)> forms = [];

    /// <summary>Records the form at <paramref name="position"/>, read as <paramref name="marker"/>. Reading it again records nothing more.</summary>
    /// <param name="position">Where the form is.</param>
    /// <param name="marker">The ASCII character it's read as.</param>
    /// <param name="escapable">Whether a backslash before it would make it text, which is so only for the marker that starts a line.</param>
    public void Record(int position, char marker, bool escapable = false) => forms.TryAdd(position, (marker, escapable));

    /// <summary>One diagnostic per run of adjacent forms, each with its line rewritten with every form on it replaced by its marker.</summary>
    public IEnumerable<Diagnostic> Report(SourceText source)
    {
        string text = source.Text;
        List<(int Start, int End)> runs = [];
        foreach (int position in forms.Keys)
        {
            if (runs is [.., var last] && last.End == position)
                runs[^1] = (last.Start, position + 1);
            else
                runs.Add((position, position + 1));
        }

        foreach ((int start, int end) in runs)
        {
            string written = text[start..end];
            string codePoints = string.Join(", ", written.Distinct().Select(c => $"U+{(int)c:X4}"));
            string marker = string.Concat(Enumerable.Range(start, end - start).Select(position => forms[position].Marker));
            string help = $"Write `{Corrected(source, start)}`, typing markers in your input method's direct (half-width) mode.";
            int escape = Enumerable.Range(0, written.Length).FirstOrDefault(index => CodeLexer.IsEscapable(written, index), -1);
            if (forms[start].Escapable && escape >= 0)
                help += " " + DiagnosticCatalog.EscapeHelp(written, written[escape]);

            yield return DiagnosticCatalog.InputMethodForm.Create(source.GetLocation(new(start, end - start)), written, codePoints, marker, help);
        }
    }

    private string Corrected(SourceText source, int position)
    {
        TextSpan line = source.GetLineSpan(source.GetLinePosition(position).Line);
        var corrected = new StringBuilder(source.Text, line.Start, line.Length, line.Length);
        foreach ((int at, (char marker, _)) in forms.Where(form => form.Key >= line.Start && form.Key < line.End))
            corrected[at - line.Start] = marker;

        return Whitespace.Trim(corrected.ToString()).ToString();
    }
}
