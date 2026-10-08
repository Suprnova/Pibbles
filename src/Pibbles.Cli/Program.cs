using System.CommandLine;
using System.CommandLine.Help;
using System.Text;
using Pibbles.Cli.Commands;
using Pibbles.Cli.Output;
using Pibbles.Cli.Projects;
using Pibbles.Semantics;

// Stories and messages hold any character, and Windows consoles otherwise use an old code page that mangles them.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// Color only when writing to a terminal, and never when NO_COLOR is set (no-color.org). Messages to people go through
// a StyledWriter, which renders their markup; machine-readable output never does.
bool noColor = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
bool outputColor = !Console.IsOutputRedirected && !noColor;
var messages = new StyledWriter(Console.Out, outputColor);
var errors = new StyledWriter(Console.Error, !Console.IsErrorRedirected && !noColor);

var root = new Argument<string?>("folder")
{
    Description = "The project's folder, with pibbles.json and the story folder in it. Leave it out to check the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var format = new Option<OutputFormat>("--format")
{
    Description = "How to print problems: pretty for people, msbuild for editors and CI, or json for tools. Leave it out for pretty.",
    HelpName = "pretty|msbuild|json",
};
var warnAsError = new Option<bool>("--warnaserror") { Description = "Fail on warnings, as well as errors." };
var release = new Option<bool>("--release") { Description = "Check what a release build ships. In v1, that's the whole story." };
var style = new Option<bool>("--style") { Description = "Also show style hints." };

var check = new Command("check", "Check the story in a project's folder, or the folder you're in, for problems.") { root, format, warnAsError, release, style };
check.SetAction(result =>
{
    if (StoryFolder.Load(result.GetValue(root) ?? ".", Directory.GetCurrentDirectory(), errors) is not { } sources)
        return Check.CouldNotRun;

    CompilationOptions settings = EditorConfigSettings.Load(sources, Directory.GetCurrentDirectory(), errors);
    return Check.Run(sources, new(result.GetValue(format), result.GetValue(warnAsError), result.GetValue(style), outputColor), Console.Out, settings);
});

var folder = new Argument<string?>("folder")
{
    Description = "Where to start the project. It's made if it doesn't exist yet. Leave it out to use the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var blank = new Option<bool>("--blank") { Description = "Start with an empty story folder, instead of an example story that explains itself." };
var init = new Command("init", "Start a new project in a folder, or the folder you're in: a pibbles.json and a story folder with an example story.") { folder, blank };
init.SetAction(result => Init.Run(result.GetValue(folder) ?? ".", Directory.GetCurrentDirectory(), result.GetValue(blank), Random.Shared, messages, errors));

var idsRoot = new Argument<string?>("folder")
{
    Description = "The project's folder, with pibbles.json and the story folder in it. Leave it out to use the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var ids = new Command("ids", "Add a line ID to every line that needs one, at the end of the line. Run it before committing.") { idsRoot };
ids.SetAction(result => Ids.Run(result.GetValue(idsRoot) ?? ".", Directory.GetCurrentDirectory(), Random.Shared, messages, errors));

var upgradeRoot = new Argument<string?>("folder")
{
    Description = "The project's folder, with pibbles.json in it. Leave it out to use the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var upgrade = new Command("upgrade", "Rewrite the project's pibbles.json in the newest format, keeping its settings.") { upgradeRoot };
upgrade.SetAction(result => Upgrade.Run(result.GetValue(upgradeRoot) ?? ".", Directory.GetCurrentDirectory(), messages, errors));

var playRoot = new Argument<string?>("folder")
{
    Description = "The project's folder, with pibbles.json and the story folder in it. Leave it out to use the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var script = new Option<string?>("--script") { Description = "Play a script, or an earlier transcript, and print the transcript. Without it, you play, answering in the terminal.", HelpName = "file" };
var start = new Option<string?>("--start") { Description = "The node to start at. With --script, only needed when the script has no start, and it replaces the script's own.", HelpName = "node" };
var set = new Option<string[]>("--set") { Description = "Set a variable before the start, like '$has_key=true'. Repeat it for more. It wins over the script's own.", HelpName = "$var=value" };
var stub = new Option<string[]>("--stub") { Description = "What a host function returns, like 'has_item=true' or 'has_item(\"key\")=false'. Repeat it for more. It wins over the script's own and the stubs file.", HelpName = "fn=value" };
var stubs = new Option<string?>("--stubs") { Description = "A file of stub lines, like 'stub has_item = true'. They win over the script's own.", HelpName = "file" };
var record = new Option<string?>("--record") { Description = "Write the session to a file as a transcript, which plays the same again with --script.", HelpName = "file" };
var noPause = new Option<bool>("--no-pause") { Description = "Print straight through to the next choice, instead of waiting for Enter after each line." };
var play = new Command("play", "Play the story in the terminal, or play a script and print the transcript.") { playRoot, script, start, set, stub, stubs, record, noPause };
play.SetAction(result =>
{
    // Ctrl+C still ends the process, with the system's interrupt code; first, play says so and writes what it recorded.
    using var interrupted = new CancellationTokenSource();
    ConsoleCancelEventHandler stop = (_, _) => interrupted.Cancel();
    Console.CancelKeyPress += stop;
    try
    {
        var options = new PlayOptions(
            result.GetValue(playRoot) ?? ".",
            result.GetValue(script),
            result.GetValue(start),
            result.GetValue(set),
            result.GetValue(stub),
            result.GetValue(stubs),
            result.GetValue(record),
            !result.GetValue(noPause),
            outputColor);
        return Play.Run(options, Directory.GetCurrentDirectory(), Console.In, Console.Out, errors, interrupted.Token);
    }
    finally
    {
        Console.CancelKeyPress -= stop;
    }
});

var testRoot = new Argument<string?>("folder")
{
    Description = "The project's folder, with pibbles.json, the story folder and the transcripts folder in it. Leave it out to use the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var update = new Option<bool>("--update") { Description = "Rewrite each transcript that doesn't match with what the story prints now. Use it once you've decided the change is right." };
var test = new Command("test", "Replay the transcripts in the project's transcripts folder, and show any that no longer match the story.") { testRoot, update };
test.SetAction(result => Test.Run(result.GetValue(testRoot) ?? ".", Directory.GetCurrentDirectory(), result.GetValue(update), Console.Out, errors, outputColor));

var code = new Argument<string>("code") { Description = "A diagnostic code, such as PIB1011." };
var explain = new Command("explain", "Explain a diagnostic: what it means, an example, and how to fix it.") { code };
explain.SetAction(result => Explain.Run(result.GetValue(code)!, Console.Out, errors, outputColor));

// Named here rather than after the executable: installed as a tool, the CLI runs as Pibbles.Cli.dll, and help would say so.
var pibbles = new Command("pibbles", "Pibbles: checks and plays narrative scripts.") { new HelpOption(), new VersionOption(), init, check, play, test, ids, upgrade, explain };
pibbles.SetAction(result => new HelpAction().Invoke(result));

return pibbles.Parse(args).Invoke();
