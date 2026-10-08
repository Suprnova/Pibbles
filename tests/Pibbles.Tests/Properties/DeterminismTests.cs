using Pibbles.Cli.Transcripts;

namespace Pibbles.Tests.Properties;

/// <summary>
/// The determinism property: the same story, choices and function stubs always give the same transcript. Random walks over
/// the sample story and the transcript tests' sources each play twice through the transcript player, and must match.
/// </summary>
public class DeterminismTests
{
    private const int MaxSteps = 300;

    [Fact]
    public void Play_SameWalkTwice_GivesTheSameTranscript() =>
        PropertyCheck.Run(Walk.Gen, Check, iterations: 300, print: walk => walk.ToString());

    private static void Check(Walk walk)
    {
        string script = string.Join('\n', [$"start {walk.Node}", .. walk.StubValues.Select(stub => $"stub {stub.Function.Name} = {ValueText.Show(stub.Value, stub.Function.ReturnType)}")]);

        string first = TranscriptPlayer.Play(walk.Compiled, script, input: new TranscriptWalk(walk.Chooser(), MaxSteps));
        string second = TranscriptPlayer.Play(walk.Compiled, script, input: new TranscriptWalk(walk.Chooser(), MaxSteps));

        Assert.Equal(first, second);
    }
}
