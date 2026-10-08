using Pibbles.Runtime;

namespace Pibbles.Cli.Transcripts;

/// <summary>A script (or transcript) could not be played: a mistake in the script, or the story failing in a way the script can't recover from.</summary>
internal sealed class TranscriptException(string message) : Exception(message);

/// <summary>How a test walks a story at random with <see cref="TranscriptPlayer"/>.</summary>
/// <param name="Choose">Picks an available option of a choice the script has no answer for.</param>
/// <param name="MaxSteps">How many steps to play before stopping, since a random walk may never end.</param>
internal sealed record TranscriptWalk(Func<ChoiceStep, ChoiceOption> Choose, int MaxSteps);

/// <summary>
/// What a script or transcript says, read from the lines at column 0. Everything else is ignored, which is why a
/// transcript is also a valid script: the directives and the answers are the same, and the printed steps are indented.
/// </summary>
/// <param name="Starts">The nodes to start at, in order. The first starts the run; each later one starts when the dialogue before it ends, with the story's state kept, which is how a script visits a node twice.</param>
/// <param name="Directives">The directives to print at the top of a transcript: the first <c>start</c>, then every <c>set</c> and <c>stub</c>, as written, in order.</param>
/// <param name="Sets">The variables to seed, as <c>(line, variable, value)</c>.</param>
/// <param name="Stubs">The stubs, in order.</param>
/// <param name="Answers">The answers to choices, in order: <c>#id</c> or a number.</param>
internal sealed record TranscriptScript(IReadOnlyList<string> Starts, IReadOnlyList<string> Directives, IReadOnlyList<ScriptSet> Sets, IReadOnlyList<ScriptStub> Stubs, IReadOnlyList<string> Answers)
{
    /// <summary>Reads a script. Directives and answers start in column 0; steps a transcript prints are indented, so none can be mistaken for them.</summary>
    /// <exception cref="TranscriptException">There is no <c>start</c> directive, or a directive can't be read.</exception>
    public static TranscriptScript Parse(string text)
    {
        List<string> starts = [];
        List<string> directives = [];
        List<ScriptSet> sets = [];
        List<ScriptStub> stubs = [];
        List<string> answers = [];

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("start ", StringComparison.Ordinal))
            {
                starts.Add(line["start ".Length..].Trim());
                if (starts.Count == 1)
                    directives.Add(line);
            }
            else if (line.StartsWith("set ", StringComparison.Ordinal))
            {
                sets.Add(ParseSet(line));
                directives.Add(line);
            }
            else if (line.StartsWith("stub ", StringComparison.Ordinal))
            {
                stubs.Add(ParseStub(line));
                directives.Add(line);
            }
            else if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                answers.Add(line[2..].Trim());
            }
        }

        return starts.Count > 0 ? new(starts, directives, sets, stubs, answers) : throw new TranscriptException("The script has no `start <node>` line.");
    }

    private static ScriptSet ParseSet(string line)
    {
        string body = line["set ".Length..];
        int equals = body.IndexOf('=', StringComparison.Ordinal);
        string variable = equals > 0 ? body[..equals].Trim() : "";
        return variable.StartsWith('$') && variable.Length > 1
            ? new(variable[1..], body[(equals + 1)..].Trim(), line)
            : throw new TranscriptException($"I can't read `{line}`. Write `set $name = value`.");
    }

    private static ScriptStub ParseStub(string line)
    {
        string body = line["stub ".Length..];
        int equals = FindEquals(body);
        if (equals <= 0)
            throw new TranscriptException($"I can't read `{line}`. Write `stub name(arguments) = value`, or `stub name = value`.");

        string head = body[..equals].Trim();
        string value = body[(equals + 1)..].Trim();
        int open = head.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
            return new(head, null, value, line);

        return head.EndsWith(')')
            ? new(head[..open].Trim(), SplitArguments(head[(open + 1)..^1]), value, line)
            : throw new TranscriptException($"I can't read `{line}`: the arguments need a closing `)`.");
    }

    /// <summary>The first <c>=</c> that isn't inside quotes.</summary>
    private static int FindEquals(string text)
    {
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '\\' && quoted)
                i++;
            else if (text[i] is '"')
                quoted = !quoted;
            else if (text[i] is '=' && !quoted)
                return i;
        }

        return -1;
    }

    /// <summary>Splits arguments at the commas that aren't inside quotes.</summary>
    private static List<string> SplitArguments(string text)
    {
        List<string> arguments = [];
        if (text.Trim().Length == 0)
            return arguments;

        bool quoted = false;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '\\' && quoted)
                i++;
            else if (text[i] is '"')
                quoted = !quoted;
            else if (text[i] is ',' && !quoted)
            {
                arguments.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        arguments.Add(text[start..].Trim());
        return arguments;
    }
}

/// <summary><c>set $variable = value</c>: seeds a variable before the dialogue starts.</summary>
internal sealed record ScriptSet(string Variable, string Value, string Line);

/// <summary><c>stub fn(arguments) = value</c>, or <c>stub fn = value</c> (with <see langword="null"/> arguments) for the fallback.</summary>
internal sealed record ScriptStub(string Function, IReadOnlyList<string>? Arguments, string Value, string Line);
