using Pibbles.Runtime;

namespace Pibbles.Tests.Runtime;

public class CommandInvocationTests
{
    private enum Mood
    {
        calm,
        tense,
    }

    private enum Other
    {
        elsewhere,
    }

    [Fact]
    public void Accessors_EveryType_ReadTheirArguments()
    {
        CommandInvocation command = Run("@all true \"x\" 2.5 1.5s mira t.n tense\n");

        Assert.Equal("all", command.Name);
        Assert.True(command.GetBool("b"));
        Assert.Equal("x", command.GetString("s"));
        Assert.Equal(2.5m, command.GetNumber("n"));
        Assert.Equal(TimeSpan.FromSeconds(1.5), command.GetDuration("d"));
        Assert.Equal("mira", command.GetActor("a"));
        Assert.Equal("t.n", command.GetNode("node"));
        Assert.Equal("tense", command.GetEnum("e"));
        Assert.Equal(Mood.tense, command.GetEnum<Mood>("e"));
    }

    [Fact]
    public void Accessors_OmittedArgument_HasItsDefault()
    {
        CommandInvocation command = Run("@all false \"\" 0 0s mira t.n\n");

        Assert.Equal("calm", command.GetEnum("e"));
        Assert.Equal(Mood.calm, command.GetEnum<Mood>("e"));
    }

    [Fact]
    public void Accessors_OmittedArgumentOfAShortCommand_HasItsDefault()
    {
        CommandInvocation command = Run("@shake\n");

        Assert.Equal(1m, command.GetNumber("strength"));
        Assert.Equal(TimeSpan.FromSeconds(0.3), command.GetDuration("duration"));
    }

    [Fact]
    public void Accessors_Misuse_Throws()
    {
        CommandInvocation command = Run("@all true \"x\" 2.5 1.5s mira t.n tense\n");

        Assert.Throws<ArgumentException>(() => command.GetNumber("missing"));
        Assert.Throws<InvalidOperationException>(() => command.GetString("b"));
        Assert.Throws<InvalidOperationException>(() => command.GetNumber("d"));
        Assert.Throws<InvalidOperationException>(() => command.GetEnum("s"));
        Assert.Throws<InvalidOperationException>(() => command.GetActor("node"));
        Assert.Throws<InvalidOperationException>(() => command.GetEnum<Other>("e"));
    }

    [Fact]
    public void GetDuration_BeyondTimeSpan_ClampsAndWarnsRightAway()
    {
        Game game = Game.Of("== t.n\n@shake duration=79228162514264337593543950335s\n");
        game.Runner.Start("t.n");
        var step = (CommandStep)game.Runner.Next();

        TimeSpan duration = step.Command.GetDuration("duration");

        Assert.Equal(TimeSpan.MaxValue, duration);
        Assert.Equal(RuntimeWarningKind.Overflow, Assert.Single(game.Warnings).Kind);
    }

    private static CommandInvocation Run(string statement)
    {
        Game game = Game.Of("== t.n\n" + statement);
        game.Runner.Start("t.n");
        return ((CommandStep)game.Runner.Next()).Command;
    }
}
