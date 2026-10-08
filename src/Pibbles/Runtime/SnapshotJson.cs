using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pibbles.Runtime;

/// <summary>
/// Writes snapshots as JSON and reads them back, exactly: numbers and durations keep every digit. It uses source-generated
/// serialization, so it's safe to trim and to compile ahead of time. Where the JSON is stored is up to the host.
/// </summary>
public static class SnapshotJson
{
    /// <summary>Writes a state snapshot as JSON.</summary>
    public static string Serialize(StateSnapshot snapshot) => JsonSerializer.Serialize(snapshot, SnapshotJsonContext.Default.StateSnapshot);

    /// <summary>Writes a runner snapshot as JSON.</summary>
    public static string Serialize(RunnerSnapshot snapshot) => JsonSerializer.Serialize(snapshot, SnapshotJsonContext.Default.RunnerSnapshot);

    /// <summary>Reads a state snapshot that <see cref="Serialize(StateSnapshot)"/> wrote.</summary>
    /// <exception cref="JsonException">The JSON isn't a state snapshot.</exception>
    public static StateSnapshot DeserializeState(string json) =>
        JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.StateSnapshot) ?? throw new JsonException("The JSON is null, not a state snapshot.");

    /// <summary>Reads a runner snapshot that <see cref="Serialize(RunnerSnapshot)"/> wrote.</summary>
    /// <exception cref="JsonException">The JSON isn't a runner snapshot.</exception>
    public static RunnerSnapshot DeserializeRunner(string json) =>
        JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.RunnerSnapshot) ?? throw new JsonException("The JSON is null, not a runner snapshot.");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
[JsonSerializable(typeof(StateSnapshot))]
[JsonSerializable(typeof(RunnerSnapshot))]
internal sealed partial class SnapshotJsonContext : JsonSerializerContext;
