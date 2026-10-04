using System.CommandLine;
using Pibbles.Cli;

var root = new Argument<string>("root")
{
    Description = "The project root: the folder with pibbles.json and the story folder.",
    DefaultValueFactory = _ => ".",
};
var format = new Option<OutputFormat>("--format")
{
    Description = "How to print problems: pretty for people, msbuild for editors and CI, json for tools.",
    DefaultValueFactory = _ => OutputFormat.Pretty,
};
var warnAsError = new Option<bool>("--warnaserror") { Description = "Fail on warnings, as well as errors." };
var release = new Option<bool>("--release") { Description = "Check what a release build ships. In v1, that's the whole story." };
var style = new Option<bool>("--style") { Description = "Also show style hints." };

var check = new Command("check", "Check the story for problems.") { root, format, warnAsError, release, style };
check.SetAction(result =>
{
    if (StoryFolder.Load(result.GetValue(root)!, Directory.GetCurrentDirectory(), Console.Error) is not { } sources)
        return Check.CouldNotRun;

    bool color = !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    return Check.Run(sources, new(result.GetValue(format), result.GetValue(warnAsError), result.GetValue(style), color), Console.Out);
});

var code = new Argument<string>("code") { Description = "A diagnostic code, such as PIB1011." };
var explain = new Command("explain", "Explain a diagnostic: what it means, an example, and how to fix it.") { code };
explain.SetAction(result => Explain.Run(result.GetValue(code)!, Console.Out, Console.Error));

return new RootCommand("Pibbles: checks and plays narrative scripts.") { check, explain }.Parse(args).Invoke();
