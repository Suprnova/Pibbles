using System.Runtime.CompilerServices;

namespace Pibbles.Tests.Snapshots;

internal static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize() => VerifierSettings.UseUtf8NoBom();
}
