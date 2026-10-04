using System.CommandLine;
using System.CommandLine.Help;
using System.Text;
using Pibbles.Cli.Commands;
using Pibbles.Cli.Output;
using Pibbles.Cli.Projects;

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

    return Check.Run(sources, new(result.GetValue(format), result.GetValue(warnAsError), result.GetValue(style), outputColor), Console.Out);
});

var folder = new Argument<string?>("folder")
{
    Description = "Where to start the project. It's made if it doesn't exist yet. Leave it out to use the folder you're in.",
    Arity = ArgumentArity.ZeroOrOne,
};
var blank = new Option<bool>("--blank") { Description = "Start with an empty story folder, instead of an example story that explains itself." };
var init = new Command("init", "Start a new project in a folder, or the folder you're in: a pibbles.json and a story folder with an example story.") { folder, blank };
init.SetAction(result => Init.Run(result.GetValue(folder) ?? ".", Directory.GetCurrentDirectory(), result.GetValue(blank), messages, errors));

var code = new Argument<string>("code") { Description = "A diagnostic code, such as PIB1011." };
var explain = new Command("explain", "Explain a diagnostic: what it means, an example, and how to fix it.") { code };
explain.SetAction(result => Explain.Run(result.GetValue(code)!, Console.Out, errors, outputColor));

var pibbles = new RootCommand("Pibbles: checks and plays narrative scripts.") { init, check, explain };
pibbles.SetAction(result => new HelpAction().Invoke(result));

return pibbles.Parse(args).Invoke();
