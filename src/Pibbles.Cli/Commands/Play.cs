using Pibbles.Cli.Output;
using Pibbles.Cli.Projects;
using Pibbles.Cli.Transcripts;
using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Cli.Commands;

/// <summary>The settings of one <c>pibbles play</c> run.</summary>
/// <param name="Root">The project's folder, or a folder inside it.</param>
/// <param name="Script">A script to play, which makes the run scripted; <see langword="null"/> plays interactively.</param>
/// <param name="Start">A node to start at, replacing the script's first <c>start</c>.</param>
/// <param name="Sets"><c>$var=value</c> flags, applied after the script's own directives.</param>
/// <param name="Stubs"><c>fn=value</c> and <c>fn(args)=value</c> flags, applied after the script's directives and the stubs file.</param>
/// <param name="StubsFile">A file of <c>stub</c> lines, applied after the script's directives.</param>
/// <param name="Record">A file to write the run to, as a transcript.</param>
/// <param name="Pause">Whether interactive play waits for Enter after each line.</param>
/// <param name="Color">Whether to color the transcript and messages.</param>
internal sealed record PlayOptions(
    string Root,
    string? Script = null,
    string? Start = null,
    IReadOnlyList<string>? Sets = null,
    IReadOnlyList<string>? Stubs = null,
    string? StubsFile = null,
    string? Record = null,
    bool Pause = true,
    bool Color = false);

/// <summary>
/// <c>pibbles play</c>: plays the story in the terminal, interactively or from a script, and prints the transcript as it
/// goes. Scripted play prints exactly what the transcript tests compare, so a reproduction becomes a test by copying files.
/// </summary>
internal static class Play
{
    /// <summary>The script or the session played to its end, or the player ended an interactive session with end of input.</summary>
    public const int Played = 0;

    /// <summary>The story has errors, or the script failed: a missing stub, a call no stub matches, no answer left, or a directive that can't be read.</summary>
    public const int Failed = 1;

    /// <summary>There's nothing to play: no story, no node to start at, or a file that can't be read.</summary>
    public const int CouldNotRun = 2;

    /// <summary>Plays the story, reading the person's answers from <paramref name="input"/> when there's no script.</summary>
    /// <param name="options">What to play, and how.</param>
    /// <param name="currentDirectory">The folder paths are relative to.</param>
    /// <param name="input">Where interactive play reads answers, stub values and Enter.</param>
    /// <param name="output">Where the transcript, and interactive play's prompts, go.</param>
    /// <param name="error">Where problems and notes go, so <paramref name="output"/> stays a transcript.</param>
    /// <param name="interrupt">Cancelled when the person presses Ctrl+C. Play stops, saying so, and writes what it recorded.</param>
    public static int Run(PlayOptions options, string currentDirectory, TextReader input, TextWriter output, TextWriter error, CancellationToken interrupt = default)
    {
        if (StoryFolder.Load(options.Root, currentDirectory, error) is not { } sources)
            return CouldNotRun;

        CompilationOptions settings = EditorConfigSettings.Load(sources, currentDirectory, error);
        if (Compile(sources, settings, error, options.Color) is not { } story)
            return Failed;

        if (options.Script is null && options.Start is null)
        {
            error.WriteLine("Give a node to start at with `--start`, or a script to play with `--script`.");
            return CouldNotRun;
        }

        if (Read(options.Script, "script", currentDirectory, error) is not { } scriptText || Read(options.StubsFile, "stubs file", currentDirectory, error) is not { } stubsText)
            return CouldNotRun;

        try
        {
            TranscriptScript script = TranscriptScript.Parse(scriptText, options.Start, [.. StubLines(stubsText), .. (options.Stubs ?? []).Select(stub => "stub " + stub), .. (options.Sets ?? []).Select(set => "set " + set)]);
            return options.Script is null
                ? Interactive(story, script, options, currentDirectory, input, output, error, interrupt)
                : Scripted(story, script, options, currentDirectory, output, error);
        }
        catch (TranscriptException exception)
        {
            error.WriteLine(exception.Message);
            return Failed;
        }
    }

    /// <summary>Compiles the story. Errors print as <c>check</c> prints them; warnings are only counted, so the transcript stays clean.</summary>
    internal static Story? Compile(IReadOnlyList<SourceText> sources, CompilationOptions settings, TextWriter error, bool color)
    {
        CompileResult result = StoryCompiler.Compile(sources, settings);
        if (result.Story is { } story)
        {
            int warnings = result.Diagnostics.Count(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning);
            if (warnings > 0)
                error.WriteLine($"{warnings} warning{(warnings == 1 ? "" : "s")}; run `pibbles check` to see {(warnings == 1 ? "it" : "them")}.");

            return story;
        }

        Diagnostic[] errors = [.. result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)];
        Dictionary<string, SourceText> byPath = sources.ToDictionary(source => source.Path);
        foreach (Diagnostic diagnostic in errors)
        {
            DiagnosticFormatter.WritePretty(error, byPath[diagnostic.Location.Path], diagnostic, color);
            error.WriteLine();
        }

        error.WriteLine(DiagnosticFormatter.Summary(sources.Count, errors, color));
        return null;
    }

    private static int Scripted(Story story, TranscriptScript script, PlayOptions options, string currentDirectory, TextWriter output, TextWriter error)
    {
        TextWriter shown = options.Color ? new ColoredLines(output) : output;
        var steps = new StringWriter();
        var player = new TranscriptPlayer(story, script, new TeeWriter(shown, steps));
        shown.Write(TranscriptPlayer.Header(script.Directives));
        try
        {
            player.Run();
        }
        finally
        {
            shown.Flush();
            Record(options.Record, player, steps, currentDirectory, error);
        }

        return Played;
    }

    private static int Interactive(Story story, TranscriptScript script, PlayOptions options, string currentDirectory, TextReader input, TextWriter output, TextWriter error, CancellationToken interrupt)
    {
        TextWriter shown = options.Color ? new ColoredLines(output) : output;
        var steps = new StringWriter();
        var player = new TranscriptPlayer(story, script, new TeeWriter(shown, steps), new TerminalInput(input, new StyledWriter(output, options.Color), story, options.Pause));
        int stopped = 0;
        void Stop(string? why)
        {
            if (Interlocked.Exchange(ref stopped, 1) == 1)
                return;

            shown.Flush();
            if (why is not null)
                error.WriteLine(why);

            Record(options.Record, player, steps, currentDirectory, error);
        }

        error.WriteLine($"{(options.Pause ? "Press Enter after each line. " : "")}End of input (Ctrl+D, or Ctrl+Z then Enter on Windows) or Ctrl+C stops.");
        shown.Write(TranscriptPlayer.Header(script.Directives));
        using CancellationTokenRegistration registration = interrupt.Register(() => Stop("Interrupted, so play stopped."));
        try
        {
            if (!player.Run())
                Stop("End of input, so play stopped.");
        }
        finally
        {
            Stop(null);
        }

        return Played;
    }

    /// <summary>Writes the run so far to the record file, if there is one, with the directives in effect now on top.</summary>
    private static void Record(string? path, TranscriptPlayer player, StringWriter steps, string currentDirectory, TextWriter error)
    {
        if (path is null)
            return;

        try
        {
            File.WriteAllText(Path.GetFullPath(path, currentDirectory), player.Transcript(steps.ToString()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"I can't write the record to `{path}`: {exception.Message}");
        }
    }

    /// <summary>A file's text, or an empty string for no file. <see langword="null"/> after saying so when it can't be read.</summary>
    private static string? Read(string? path, string what, string currentDirectory, TextWriter error)
    {
        if (path is null)
            return "";

        try
        {
            return File.ReadAllText(Path.GetFullPath(path, currentDirectory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"I can't read the {what} `{path}`: {exception.Message}");
            return null;
        }
    }

    /// <summary>The lines of a stubs file, which holds only <c>stub</c> lines and blank lines.</summary>
    private static string[] StubLines(string text)
    {
        string[] lines = [.. text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Trim().Length > 0)];
        return lines.FirstOrDefault(line => !line.StartsWith("stub ", StringComparison.Ordinal)) is { } other
            ? throw new TranscriptException($"I can't read `{other}` in the stubs file: it holds only `stub` lines.")
            : lines;
    }
}
