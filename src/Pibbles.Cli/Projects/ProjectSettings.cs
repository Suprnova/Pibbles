using System.Text.Json;
using Pibbles.Cli.Output;

namespace Pibbles.Cli.Projects;

/// <summary>
/// A project's settings, from <c>pibbles.json</c> in its root. Every setting has a default, so the file, and any key in
/// it, is optional. <c>schema</c> says which version of this format the file uses, and an older schema still reads.
/// </summary>
/// <param name="StoryFolders">The folders the story's files are in, relative to the project root.</param>
internal sealed record ProjectSettings(IReadOnlyList<string> StoryFolders)
{
    /// <summary>The newest schema this version of Pibbles reads. A change to the format adds one.</summary>
    /// <remarks>Schema 1 named a single folder, <c>"story": "story"</c>. Schema 2 lists them, <c>"storyFolders": ["story"]</c>.</remarks>
    public const int Schema = 2;

    public const string FileName = "pibbles.json";

    public static ProjectSettings Default { get; } = new(["story"]);

    /// <summary>The schema the file was written in. <c>pibbles upgrade</c> rewrites an older one in the current schema.</summary>
    public int FileSchema { get; init; } = Schema;

    private static readonly string[] Keys = ["schema", "storyFolders"];

    /// <summary>Reads settings from <c>pibbles.json</c>'s text. Returns <see langword="null"/> after writing the problem to <paramref name="error"/>.</summary>
    public static ProjectSettings? Parse(string json, TextWriter error)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            error.WriteLine($"I can't read `{FileName}`: {exception.Message}");
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                error.WriteLine($"`{FileName}` has to hold its settings in braces, like {{ \"schema\": {Schema} }}.");
                return null;
            }

            if (ReadSchema(document.RootElement, error) is not { } schema)
                return null;

            ProjectSettings settings = Default with { FileSchema = schema };
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "schema":
                        break;

                    case "storyFolders" when schema >= 2 && Folders(property.Value) is { } folders:
                        settings = settings with { StoryFolders = folders };
                        break;

                    case "storyFolders" when schema >= 2:
                        error.WriteLine($"`storyFolders` in `{FileName}` has to be a list of folder names, in quotes, like [\"story\"].");
                        return null;

                    case "storyFolders":
                        error.WriteLine($"`storyFolders` in `{FileName}` needs schema 2 or later. Write `\"schema\": {Schema}`.");
                        return null;

                    case "story" when schema == 1 && property.Value.ValueKind is JsonValueKind.String && property.Value.GetString() is { Length: > 0 } story:
                        settings = settings with { StoryFolders = [story] };
                        break;

                    case "story" when schema == 1:
                        error.WriteLine($"`story` in `{FileName}` has to be a folder name, in quotes.");
                        return null;

                    case "story":
                        error.WriteLine($"`story` in `{FileName}` is `storyFolders` from schema 2 on, a list of folders: write `\"storyFolders\": [\"story\"]`.");
                        return null;

                    default:
                        error.WriteLine($"I don't know the setting `{property.Name}` in `{FileName}`. The settings are {string.Join(" and ", Keys.Select(key => $"`{key}`"))}.");
                        return null;
                }
            }

            return settings;
        }
    }

    /// <summary>The file's schema, or the current one when it doesn't say.</summary>
    private static int? ReadSchema(JsonElement root, TextWriter error)
    {
        if (!root.TryGetProperty("schema", out JsonElement value))
            return Schema;

        if (value.ValueKind is not JsonValueKind.Number || !value.TryGetInt32(out int schema) || schema < 1)
        {
            error.WriteLine($"`schema` in `{FileName}` has to be a whole number, like {Schema}.");
            return null;
        }

        if (schema > Schema)
        {
            error.WriteLine($"This project's `{FileName}` was written by a newer version of Pibbles (schema {schema}; this version reads up to {Schema}). Update Pibbles to work on it.");
            return null;
        }

        return schema;
    }

    /// <summary>A list of folder names, or <see langword="null"/> if it's empty or holds anything else.</summary>
    private static string[]? Folders(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Array)
            return null;

        string[] folders = [.. value.EnumerateArray().Select(folder => folder.ValueKind is JsonValueKind.String ? folder.GetString()! : "")];
        return folders.Length > 0 && folders.All(folder => folder.Length > 0) ? folders : null;
    }

    /// <summary>The settings as <c>pibbles.json</c> text, with the current schema.</summary>
    public string ToJson() => JsonText.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schema", Schema);
        writer.WriteStartArray("storyFolders");
        foreach (string folder in StoryFolders)
            writer.WriteStringValue(folder);

        writer.WriteEndArray();
        writer.WriteEndObject();
    }) + "\n";
}
