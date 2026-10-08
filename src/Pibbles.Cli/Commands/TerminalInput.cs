using Pibbles.Cli.Transcripts;
using Pibbles.Compiler;
using Pibbles.Runtime;

namespace Pibbles.Cli.Commands;

/// <summary>
/// A person playing at a terminal: answers choices by number or <c>#id</c>, types the value of a call no stub covers, and,
/// with <paramref name="pause"/>, presses Enter after each line. An answer or value that doesn't fit says why and asks
/// again. End of input stops the run.
/// </summary>
/// <param name="input">Where the person types.</param>
/// <param name="prompts">Where the questions and the reasons for asking again go.</param>
/// <param name="story">The story, for the enum members, actors and nodes a value can name.</param>
/// <param name="pause">Whether to wait for Enter after each line.</param>
internal sealed class TerminalInput(TextReader input, TextWriter prompts, Story story, bool pause) : IPlayInput
{
    public bool SuppliesStubs => true;

    public ChoiceOption? Choose(ChoiceStep choice)
    {
        while (Ask("Pick an option by its number or `#id`: ") is { } answer)
        {
            try
            {
                return TranscriptPlayer.Pick(choice, answer);
            }
            catch (TranscriptException exception)
            {
                prompts.WriteLine(exception.Message);
            }
        }

        return null;
    }

    public object? Stub(FunctionInfo function, IReadOnlyList<object?> arguments)
    {
        string call = ValueText.Call(function, arguments);
        while (Ask($"Nothing stubs `{call}`. What does it return? ({function.ReturnType}): ") is { } value)
        {
            try
            {
                return ValueText.Parse(value, function.ReturnType, story, $"stub {call} = {value}");
            }
            catch (TranscriptException exception)
            {
                prompts.WriteLine(exception.Message);
            }
        }

        return null;
    }

    public bool Continue(DialogueStep step) => !pause || step is not LineStep || input.ReadLine() is not null;

    /// <summary>Asks a question until the answer isn't blank, and returns it trimmed, or <see langword="null"/> at the end of input.</summary>
    private string? Ask(string question)
    {
        while (true)
        {
            prompts.Write(question);
            prompts.Flush();
            if (input.ReadLine() is not { } answer)
            {
                prompts.WriteLine();
                return null;
            }

            if (answer.Trim().Length > 0)
                return answer.Trim();
        }
    }
}
