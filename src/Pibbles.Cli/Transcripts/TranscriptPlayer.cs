using Pibbles.Compiler;
using Pibbles.Runtime;
using Pibbles.Semantics;
using System.Globalization;
using System.Text;

namespace Pibbles.Cli.Transcripts;

/// <summary>
/// Plays a script against a story and writes the transcript: every step, with its markers, spans and icons in the text,
/// every variable change before the step that followed it, every warning, and every answer. A transcript is also a
/// valid script, so a run can be copied into a test as it is.
/// </summary>
/// <remarks>
/// Steps are printed indented by two spaces, and anything free-form in them has its backslashes and line breaks
/// escaped, so no printed line can start at column 0 and be read as a directive or an answer.
/// </remarks>
internal sealed class TranscriptPlayer
{
    private const string Indent = "  ";

    private readonly Story story;
    private readonly TranscriptScript script;
    private readonly ISet<Type>? kinds;
    private readonly StringBuilder output = new();
    private readonly List<RuntimeWarning> warnings = [];
    private readonly Dictionary<string, string> printed = [];
    private int started;
    private StoryState state = null!;
    private DialogueRunner runner = null!;

    private TranscriptPlayer(Story story, TranscriptScript script, ISet<Type>? kinds)
    {
        this.story = story;
        this.script = script;
        this.kinds = kinds;
    }

    /// <summary>Plays a script and returns the transcript.</summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="scriptText">The script, or an earlier transcript.</param>
    /// <param name="kinds">Collects the kinds of step and marker that were printed, for tests that check every kind is covered.</param>
    /// <exception cref="TranscriptException">The script can't be played: a function has no stub, a choice has no answer, or the story fails.</exception>
    public static string Play(Story story, string scriptText, ISet<Type>? kinds = null)
    {
        var player = new TranscriptPlayer(story, TranscriptScript.Parse(scriptText), kinds);
        player.Run();
        return player.output.ToString();
    }

    private void Run()
    {
        HostFunctions functions = Stubs();
        state = new(story, 0);
        foreach (ScriptSet set in script.Sets)
        {
            VariableSymbol variable = story.Variables.Select(candidate => candidate.Variable).FirstOrDefault(candidate => candidate.Name == set.Variable)
                ?? throw new TranscriptException($"I can't read `{set.Line}`: the story has no variable called `${set.Variable}`.");
            state.Variables[variable] = ValueText.Parse(set.Value, variable.Type, story, set.Line);
        }

        runner = new(story, state, functions, new() { OnWarning = warnings.Add });
        StartNext();

        foreach (string directive in script.Directives)
            output.Append(directive).Append('\n');

        output.Append('\n');
        PrintVariableChanges();
        Queue<string> answers = new(script.Answers);
        while (true)
        {
            DialogueStep step = Next();
            PrintVariableChanges();
            PrintWarnings();
            Print(step);
            switch (step)
            {
                case EndStep when started < script.Starts.Count:
                    output.Append("\nstart ").Append(script.Starts[started]).Append('\n');
                    StartNext();
                    break;
                case EndStep:
                    return;
                case ChoiceStep choice:
                    Answer(choice, answers);
                    break;
            }
        }
    }

    private void StartNext()
    {
        try
        {
            runner.Start(script.Starts[started++]);
        }
        catch (InvalidOperationException exception)
        {
            throw new TranscriptException(exception.Message);
        }
    }

    private DialogueStep Next()
    {
        try
        {
            return runner.Next();
        }
        catch (HostFunctionException exception)
        {
            throw new TranscriptException(exception.InnerException is TranscriptException inner ? inner.Message : exception.Message);
        }
    }

    private void Answer(ChoiceStep choice, Queue<string> answers)
    {
        IReadOnlyList<string> sourceOrder = runner.WaitingChoiceIds;
        if (answers.Count == 0)
            throw new TranscriptException($"The script has no answer for the choice with options {string.Join(", ", choice.Options.Select(option => Name(option.Id)))}.");

        string answer = answers.Dequeue();
        ChoiceOption? picked = (answer.StartsWith('#')
            ? choice.Options.FirstOrDefault(option => option.Id == answer[1..])
            : int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= sourceOrder.Count
                ? choice.Options.FirstOrDefault(option => option.Id == sourceOrder[number - 1])
                : null) ?? throw new TranscriptException($"The answer `> {answer}` isn't one of the options on offer: {string.Join(", ", choice.Options.Select(option => $"{sourceOrder.ToList().IndexOf(option.Id) + 1} ({Name(option.Id)})"))}.");
        if (!picked.IsAvailable)
            throw new TranscriptException($"The answer `> {answer}` is an option that isn't available.");

        output.Append(story.FallbackIds.Contains(picked.Id) ? $"> {sourceOrder.ToList().IndexOf(picked.Id) + 1}" : $"> #{picked.Id}").Append('\n');
        runner.Choose(picked);
    }

    private string Name(string id) => story.FallbackIds.Contains(id) ? $"option {id}" : $"#{id}";

    private void PrintVariableChanges()
    {
        foreach (StoryVariable variable in story.Variables)
        {
            string shown = ValueText.Show(state.Variables[variable.Variable]);
            if (printed.TryGetValue(variable.Variable.Name, out string? before))
            {
                if (before != shown)
                    output.Append(Indent).Append('$').Append(variable.Variable.Name).Append(" = ").Append(shown).Append('\n');
            }

            printed[variable.Variable.Name] = shown;
        }
    }

    private void PrintWarnings()
    {
        foreach (RuntimeWarning warning in warnings)
        {
            output.Append(Indent).Append("warning ").Append(warning.Kind).Append(" (").Append(warning.Location.Path).Append(" line ")
                .Append(warning.Location.Start.Line + 1).Append("): ").Append(ValueText.Escape(warning.Message)).Append('\n');
        }

        warnings.Clear();
    }

    private void Print(DialogueStep step)
    {
        kinds?.Add(step.GetType());
        switch (step)
        {
            case LineStep line:
                output.Append(Indent).Append(line.Line.Speaker ?? "narration").Append(": ").Append(Text(line.Line)).Append(Suffix(line.Line)).Append('\n');
                break;

            case ChoiceStep choice:
                output.Append(Indent).Append("choice\n");
                IReadOnlyList<string> sourceOrder = runner.WaitingChoiceIds;
                foreach (ChoiceOption option in choice.Options)
                {
                    output.Append(Indent).Append(Indent).Append(sourceOrder.ToList().IndexOf(option.Id) + 1).Append(". ").Append(Text(option.Text)).Append(Suffix(option.Text))
                        .Append(option.IsAvailable ? "" : " (unavailable)").Append(option.WasChosen ? " (chosen)" : "").Append('\n');
                }

                break;

            case CommandStep command:
                output.Append(Indent).Append('@').Append(command.Command.Name).Append(Arguments(command.Command.Values)).Append(command.Waits ? " waits" : "").Append('\n');
                break;

            case PoseStep pose:
                output.Append(Indent).Append("pose ").Append(pose.Actor).Append(' ').Append(pose.Pose).Append('\n');
                break;

            case WaitStep wait:
                output.Append(Indent).Append("wait ").Append(LineRenderer.FormatNumber((decimal)wait.Duration.Ticks / 10_000_000m)).Append("s\n");
                break;

            case EndStep:
                output.Append(Indent).Append("end\n");
                break;
        }
    }

    private static string Arguments(IEnumerable<Value> values) => string.Concat(values.Select(value => " " + ValueText.Show(value)));

    /// <summary>A line's tags and ID after its text. An ID the compiler made up is written <c>#~</c>, since it changes whenever lines move.</summary>
    private string Suffix(Line line)
    {
        string tags = string.Concat(line.Tags.Where(tag => tag.Name != "id").Select(tag => " #" + tag.Name + (tag.Value is null ? "" : ":" + tag.Value)));
        return tags + "  " + (story.FallbackIds.Contains(line.Id) ? "#~" : "#" + line.Id);
    }

    /// <summary>A line's text with its spans, markers and icons written in at their positions.</summary>
    private string Text(Line line)
    {
        var text = new StringBuilder();
        Stack<Span> open = [];
        int nextSpan = 0;
        int nextMarker = 0;
        for (int position = 0; position <= line.Text.Length; position++)
        {
            while (open.Count > 0 && open.Peek().End == position)
                text.Append("[/").Append(open.Pop().Name).Append(']');

            for (; nextMarker < line.Markers.Length && line.Markers[nextMarker].Position == position; nextMarker++)
                text.Append(Marker(line.Markers[nextMarker]));

            for (; nextSpan < line.Spans.Length && line.Spans[nextSpan].Start == position; nextSpan++)
            {
                Span span = line.Spans[nextSpan];
                text.Append('[').Append(span.Name).Append(Arguments(span.Arguments.Values)).Append(']');
                if (span.Length == 0)
                    text.Append("[/").Append(span.Name).Append(']');
                else
                    open.Push(span);
            }

            if (position == line.Text.Length)
                break;

            Icon? icon = line.Icons.FirstOrDefault(candidate => candidate.Position == position);
            text.Append(icon is null ? ValueText.Escape(line.Text[position].ToString()) : $"⟨icon {icon.Name}⟩");
        }

        return text.ToString();
    }

    private string Marker(Marker marker)
    {
        kinds?.Add(marker.GetType());
        return marker switch
        {
            InputWaitMarker => "⟨w⟩",
            PauseMarker pause => $"⟨w {LineRenderer.FormatNumber((decimal)pause.Duration.Ticks / 10_000_000m)}s⟩",
            PageBreakMarker => "⟨p⟩",
            SpeedMarker { Factor: 1 } => "⟨speed⟩",
            SpeedMarker speed => $"⟨speed {LineRenderer.FormatNumber(speed.Factor)}⟩",
            CommandMarker command => $"⟨@{command.Command.Name}{Arguments(command.Command.Values)}{(command.Waits ? " wait" : "")}⟩",
            _ => throw new NotSupportedException(marker.GetType().Name),
        };
    }

    /// <summary>Registers a stub for each function the story calls, and fails, listing every missing one, if any has none.</summary>
    private HostFunctions Stubs()
    {
        FunctionSymbol[] called = [.. StoryCalls.Functions(story)];
        string[] missing = [.. called.Select(function => function.Name).Where(name => script.Stubs.All(stub => stub.Function != name))];
        if (missing.Length > 0)
            throw new TranscriptException($"The story calls {string.Join(", ", missing.Select(name => $"`{name}`"))}, but the script has no stub for {(missing.Length == 1 ? "it" : "them")}. Add a line like `stub {missing[0]} = value`.");

        var functions = new HostFunctions();
        foreach (IGrouping<string, ScriptStub> group in script.Stubs.GroupBy(stub => stub.Function))
        {
            FunctionSymbol function = story.Functions.GetValueOrDefault(group.Key)
                ?? throw new TranscriptException($"I can't read `{group.First().Line}`: the story doesn't declare a function called `{group.Key}`.");

            Dictionary<string, object> exact = [];
            object? fallback = null;
            bool hasFallback = false;
            foreach (ScriptStub stub in group)
            {
                object result = ValueText.ToHost(ValueText.Parse(stub.Value, function.ReturnType, story, stub.Line));
                if (stub.Arguments is null)
                {
                    fallback = result;
                    hasFallback = true;
                    continue;
                }

                if (stub.Arguments.Count != function.Parameters.Count)
                    throw new TranscriptException($"I can't read `{stub.Line}`: `{function.Name}` takes {function.Parameters.Count} argument{(function.Parameters.Count == 1 ? "" : "s")}.");

                string key = string.Join('\u0001', stub.Arguments.Select((argument, index) => ValueText.Key(ValueText.ToHost(ValueText.Parse(argument, function.Parameters[index].Type, story, stub.Line)))));
                exact[key] = result;
            }

            functions.AddUntyped(
                function.Name,
                [.. function.Parameters.Select(parameter => HostFunctions.ClrTypeOf(parameter.Type))],
                HostFunctions.ClrTypeOf(function.ReturnType),
                arguments => exact.TryGetValue(string.Join('\u0001', arguments.Select(ValueText.Key)), out object? result) ? result
                    : hasFallback ? fallback
                    : throw new TranscriptException($"The script has no stub for `{function.Name}({string.Join(", ", arguments.Select(argument => argument is string text ? ValueText.Show(Value.String(text)) : ValueText.Key(argument)[1..]))})`."));
        }

        return functions;
    }
}
