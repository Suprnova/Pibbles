using System.Globalization;
using System.Text;
using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Cli.Transcripts;

/// <summary>
/// Plays a script against a story and writes the transcript: every step, with its markers, spans and icons in the text,
/// every variable change before the step that followed it, every warning, and every answer. A transcript is also a
/// valid script, so a run can be copied into a test as it is. It uses only the library's public API, like any host.
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
    private readonly TextWriter output;
    private readonly IPlayInput? input;
    private readonly ISet<Type>? kinds;
    private readonly List<RuntimeWarning> warnings = [];
    private readonly Dictionary<string, string> printed = [];
    private readonly List<string> supplied = [];
    private readonly HashSet<string> called = [];
    private int started;
    private StoryState state = null!;
    private DialogueRunner runner = null!;

    /// <summary>
    /// Prepares to play a script, writing the steps to <paramref name="output"/> as they happen. The directives aren't
    /// written, since the input can add stubs as the run goes; <see cref="Transcript"/> puts them on top.
    /// </summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="script">The script, or an earlier transcript.</param>
    /// <param name="output">Where the steps go.</param>
    /// <param name="input">Answers what the script doesn't: a person at a terminal, or a test's random walk.</param>
    /// <param name="kinds">Collects the kinds of step and marker that were printed, for tests that check every kind is covered.</param>
    public TranscriptPlayer(Story story, TranscriptScript script, TextWriter output, IPlayInput? input = null, ISet<Type>? kinds = null)
    {
        this.story = story;
        this.script = script;
        this.output = output;
        this.input = input;
        this.kinds = kinds;
    }

    /// <summary>
    /// The transcript's directives, which go above its steps: the script's, then a <c>stub</c> line for every value the
    /// input has supplied, then, when the input supplies stubs, a <c>stub</c> with the default value for each function the
    /// run hasn't called, so that replaying the transcript as a script plays the same run.
    /// </summary>
    public IReadOnlyList<string> Directives => [.. script.Directives, .. supplied, .. Defaults()];

    /// <summary>Plays a script and returns the transcript.</summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="scriptText">The script, or an earlier transcript.</param>
    /// <param name="kinds">Collects the kinds of step and marker that were printed, for tests that check every kind is covered.</param>
    /// <param name="input">Answers what the script doesn't, for tests that walk a story at random.</param>
    /// <exception cref="TranscriptException">The script can't be played: a function has no stub, a choice has no answer, or the story fails.</exception>
    public static string Play(Story story, string scriptText, ISet<Type>? kinds = null, IPlayInput? input = null)
    {
        var steps = new StringWriter();
        var player = new TranscriptPlayer(story, TranscriptScript.Parse(scriptText), steps, input, kinds);
        player.Run();
        return player.Transcript(steps.ToString());
    }

    /// <summary>The whole transcript: the <see cref="Directives"/>, a blank line, and the steps.</summary>
    public string Transcript(string steps) => Header(Directives) + steps;

    /// <summary>Directives as they go above a transcript's steps, each on its line, then a blank line.</summary>
    public static string Header(IEnumerable<string> directives) => string.Concat(directives.Select(directive => directive + "\n")) + "\n";

    /// <summary>Picks the option an answer names, by <c>#id</c> or by its source number, if it's available.</summary>
    /// <exception cref="TranscriptException">The answer names no option on offer, or one that isn't available.</exception>
    public static ChoiceOption Pick(ChoiceStep choice, string answer)
    {
        ChoiceOption? picked = answer.StartsWith('#')
            ? choice.Options.FirstOrDefault(option => option.Id == answer[1..])
            : int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? choice.Options.FirstOrDefault(option => option.Number == number) : null;
        if (picked is null)
            throw new TranscriptException($"The answer `> {answer}` isn't one of the options on offer: {string.Join(", ", choice.Options.Select(option => $"{option.Number} ({Name(option)})"))}.");

        return picked.IsAvailable ? picked : throw new TranscriptException($"The answer `> {answer}` is an option that isn't available.");
    }

    /// <summary>Plays to the end, or until the input stops it. Call it once.</summary>
    /// <returns>Whether the run reached its end, as opposed to its input stopping it.</returns>
    /// <exception cref="TranscriptException">The script can't be played: a function has no stub, a choice has no answer, or the story fails.</exception>
    public bool Run()
    {
        HostFunctions functions = Stubs();
        state = new(story, 0);
        foreach (ScriptSet set in script.Sets)
        {
            VariableInfo variable = story.Variables.FirstOrDefault(candidate => candidate.Name == set.Variable)
                ?? throw new TranscriptException($"I can't read `{set.Line}`: the story has no variable called `${set.Variable}`.");
            state.SetVariable(variable.Name, ValueText.Parse(set.Value, variable.Type, story, set.Line));
        }

        runner = new(story, state, functions, new() { OnWarning = warnings.Add });
        StartNext();
        PrintVariableChanges();
        Queue<string> answers = new(script.Answers);
        while (true)
        {
            if (Next() is not { } step)
                return false;

            PrintVariableChanges();
            PrintWarnings();
            Print(step);
            if (input?.Continue(step) is false)
                return false;

            switch (step)
            {
                case EndStep when started < script.Starts.Count:
                    WriteLine("");
                    WriteLine("start " + script.Starts[started]);
                    StartNext();
                    break;
                case EndStep:
                    return true;
                case ChoiceStep choice when !Answer(choice, answers):
                    return false;
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

    /// <summary>The next step, or <see langword="null"/> when the input stopped the run while a host function asked it for a value.</summary>
    private DialogueStep? Next()
    {
        try
        {
            return runner.Next();
        }
        catch (HostFunctionException exception) when (exception.InnerException is StoppedException)
        {
            return null;
        }
        catch (HostFunctionException exception)
        {
            throw new TranscriptException(exception.InnerException is TranscriptException inner ? inner.Message : exception.Message);
        }
    }

    /// <summary>Answers a choice from the script, or else from the input. Returns <see langword="false"/> when the input stopped the run instead.</summary>
    private bool Answer(ChoiceStep choice, Queue<string> answers)
    {
        ChoiceOption? picked = answers.Count > 0 ? Pick(choice, answers.Dequeue())
            : input is not null ? input.Choose(choice)
            : throw new TranscriptException($"The script has no answer for the choice with options {string.Join(", ", choice.Options.Select(Name))}.");
        if (picked is null)
            return false;

        WriteLine(picked.Text.IsFallbackId ? $"> {picked.Number}" : $"> #{picked.Id}");
        runner.Choose(picked);
        return true;
    }

    private static string Name(ChoiceOption option) => option.Text.IsFallbackId ? $"option {option.Number}" : $"#{option.Id}";

    private void WriteLine(string line)
    {
        output.Write(line);
        output.Write('\n');
    }

    private void PrintVariableChanges()
    {
        foreach (VariableInfo variable in story.Variables)
        {
            string shown = ValueText.Show(state.GetVariable(variable.Name), variable.Type);
            if (printed.TryGetValue(variable.Name, out string? before) && before != shown)
                WriteLine($"{Indent}${variable.Name} = {shown}");

            printed[variable.Name] = shown;
        }
    }

    private void PrintWarnings()
    {
        foreach (RuntimeWarning warning in warnings)
            WriteLine($"{Indent}warning {warning.Kind} ({warning.Location.Path} line {warning.Location.Start.Line + 1}): {ValueText.Escape(warning.Message)}");

        warnings.Clear();
    }

    private void Print(DialogueStep step)
    {
        kinds?.Add(step.GetType());
        switch (step)
        {
            case LineStep line:
                WriteLine($"{Indent}{line.Line.Speaker ?? "narration"}: {Text(line.Line)}{Suffix(line.Line)}");
                break;

            case ChoiceStep choice:
                WriteLine($"{Indent}choice");
                foreach (ChoiceOption option in choice.Options)
                    WriteLine($"{Indent}{Indent}{option.Number}. {Text(option.Text)}{Suffix(option.Text)}{(option.IsAvailable ? "" : " (unavailable)")}{(option.WasChosen ? " (chosen)" : "")}");

                break;

            case CommandStep command:
                WriteLine($"{Indent}@{command.Command.Name}{Arguments(command.Command.Parameters, command.Command.GetValue)}{(command.Waits ? " waits" : "")}");
                break;

            case PoseStep pose:
                WriteLine($"{Indent}pose {pose.Actor} {pose.Pose}");
                break;

            case WaitStep wait:
                WriteLine($"{Indent}wait {ValueText.FormatSeconds(wait.Duration)}");
                break;

            case EndStep:
                WriteLine($"{Indent}end");
                break;
        }
    }

    private static string Arguments(IReadOnlyList<StoryParameter> parameters, Func<string, object> get) =>
        string.Concat(parameters.Select(parameter => " " + ValueText.Show(get(parameter.Name), parameter.Type)));

    /// <summary>A line's tags and ID after its text. An ID the compiler made up is written <c>#~</c>, since it changes whenever lines move.</summary>
    private static string Suffix(Line line)
    {
        string tags = string.Concat(line.Tags.Where(tag => tag.Name != "id").Select(tag => " #" + tag.Name + (tag.Value is null ? "" : ":" + tag.Value)));
        return tags + "  " + (line.IsFallbackId ? "#~" : "#" + line.Id);
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
                text.Append('[').Append(span.Name).Append(Arguments(span.Arguments.Parameters, span.Arguments.GetValue)).Append(']');
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
            PauseMarker pause => $"⟨w {ValueText.FormatSeconds(pause.Duration)}⟩",
            PageBreakMarker => "⟨p⟩",
            SpeedMarker { Factor: 1 } => "⟨speed⟩",
            SpeedMarker speed => $"⟨speed {ValueText.FormatNumber(speed.Factor)}⟩",
            CommandMarker command => $"⟨@{command.Command.Name}{Arguments(command.Command.Parameters, command.Command.GetValue)}{(command.Waits ? " wait" : "")}⟩",
            _ => throw new NotSupportedException(marker.GetType().Name),
        };
    }

    /// <summary>
    /// Registers each declared function, answering from the script's stubs and then from the input. Without an input that
    /// supplies stubs, it fails, listing every declared function the script doesn't stub.
    /// </summary>
    private HostFunctions Stubs()
    {
        if (script.Stubs.FirstOrDefault(stub => story.Functions.All(function => function.Name != stub.Function)) is { } unknown)
            throw new TranscriptException($"I can't read `{unknown.Line}`: the story doesn't declare a function called `{unknown.Function}`.");

        var functions = new HostFunctions();
        foreach (FunctionInfo function in story.Functions)
        {
            ScriptStub[] stubs = [.. script.Stubs.Where(stub => stub.Function == function.Name)];
            if (stubs.Length > 0 || input?.SuppliesStubs is true)
                functions.AddDynamic(function.Name, [.. function.Parameters.Select(parameter => parameter.Type.HostType)], function.ReturnType.HostType, new StubTable(this, function, stubs).Call);
        }

        string[] missing = [.. functions.Validate(story).Where(problem => problem.Kind is HostFunctionProblemKind.Missing).Select(problem => problem.Function)];
        if (missing.Length > 0)
            throw new TranscriptException($"The story declares {string.Join(", ", missing.Select(name => $"`{name}`"))}, but the script has no stub for {(missing.Length == 1 ? "it" : "them")}. Add a line like `stub {missing[0]} = value`.");

        return functions;
    }

    /// <summary>A <c>stub</c> with the default value of its return type for each function the run never called and nothing stubs, when the input supplies stubs.</summary>
    private IEnumerable<string> Defaults() =>
        input?.SuppliesStubs is true
            ? story.Functions
                .Where(function => !called.Contains(function.Name) && script.Stubs.All(stub => stub.Function != function.Name))
                .Select(function => (function, Value: ValueText.Default(function.ReturnType, story)))
                .Where(stub => stub.Value is not null)
                .Select(stub => $"stub {stub.function.Name} = {ValueText.Show(stub.Value!, stub.function.ReturnType)}")
            : [];

    /// <summary>The input stopped the run while a host function asked it for a value.</summary>
    private sealed class StoppedException : Exception;

    /// <summary>One function's stubs: exact ones by their arguments, the one for any others, and then whatever the input supplies.</summary>
    private sealed class StubTable
    {
        private readonly TranscriptPlayer player;
        private readonly FunctionInfo function;
        private readonly Dictionary<string, object> exact = [];
        private readonly object? fallback;

        public StubTable(TranscriptPlayer player, FunctionInfo function, IEnumerable<ScriptStub> stubs)
        {
            this.player = player;
            this.function = function;
            foreach (ScriptStub stub in stubs)
            {
                object result = ValueText.Parse(stub.Value, function.ReturnType, player.story, stub.Line);
                if (stub.Arguments is null)
                {
                    fallback = result;
                    continue;
                }

                if (stub.Arguments.Count != function.Parameters.Count)
                    throw new TranscriptException($"I can't read `{stub.Line}`: `{function.Name}` takes {function.Parameters.Count} argument{(function.Parameters.Count == 1 ? "" : "s")}.");

                exact[Key(stub.Arguments.Select((argument, index) => ValueText.Parse(argument, function.Parameters[index].Type, player.story, stub.Line)))] = result;
            }
        }

        public object? Call(object?[] arguments)
        {
            player.called.Add(function.Name);
            string key = Key(arguments);
            if (exact.TryGetValue(key, out object? result))
                return result;

            if (fallback is not null)
                return fallback;

            string call = ValueText.Call(function, arguments);
            if (player.input?.SuppliesStubs is not true)
                throw new TranscriptException($"The script has no stub for `{call}`.");

            object supplied = player.input.Stub(function, arguments) ?? throw new StoppedException();
            exact[key] = supplied;
            player.supplied.Add($"stub {(function.Parameters.Count == 0 ? function.Name : call)} = {ValueText.Show(supplied, function.ReturnType)}");
            return supplied;
        }

        private static string Key(IEnumerable<object?> arguments) => string.Join('\u0001', arguments.Select(ValueText.Key));
    }
}
