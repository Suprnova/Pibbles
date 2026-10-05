using CsCheck;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Runs a property over a fixed sequence of generated cases, so every run checks the same inputs. When a case fails,
/// CsCheck replays it from its seed and shrinks it, and the failure names that seed.
/// </summary>
/// <remarks>
/// Setting <c>CsCheck_Iter</c>, <c>CsCheck_Time</c> or <c>CsCheck_Seed</c> hands the run to CsCheck's own random search
/// instead, for a longer run, a random one, or replaying a reported seed. <c>docs/architecture.md</c> shows how.
/// </remarks>
internal static class PropertyCheck
{
    private const ulong FixedSeed = 0x427572676572;

    private static readonly bool Searching = new[] { "CsCheck_Iter", "CsCheck_Time", "CsCheck_Seed" }.Any(name => Environment.GetEnvironmentVariable(name) is not null);

    public static void Run<T>(Gen<T> gen, Action<T> assert, long iterations, Func<T, string>? print = null)
    {
        if (Searching)
        {
            gen.Sample(assert, print: print);
            return;
        }

        var pcg = new PCG(1, FixedSeed);
        for (long i = 0; i < iterations; i++)
        {
            ulong state = pcg.State;
            T value = gen.Generate(pcg, null, out _);
            try
            {
                assert(value);
            }
            catch (Exception)
            {
                gen.Sample(assert, seed: pcg.ToString(state), iter: iterations, threads: 1, print: print);
                throw;
            }
        }
    }
}
