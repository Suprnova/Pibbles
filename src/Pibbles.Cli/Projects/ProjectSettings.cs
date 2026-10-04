using System.Text.Json;
using Pibbles.Cli.Output;

namespace Pibbles.Cli.Projects;

/// <summary>
/// A project's settings, from <c>pibbles.json</c> in its root. Every setting has a default, so the file, and any key in
/// it, is optional. <c>schema</c> says which version of this format the file uses.
/// </summary>
/// <param name="Story">The story folder, relative to the project root.</param>
internal sealed record ProjectSettings(string Story)
{
    /// <summary>The newest schema this version of Pibbles reads. Adding a key to the format adds one.</summary>
    public const int Schema = 1;

    public const string FileName = "pibbles.json";

    public static ProjectSettings Default { get; } = new("story");

    private static readonly string[] Keys = ["schema", "story"];

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

            ProjectSettings settings = Default;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "schema" when property.Value.ValueKind is not JsonValueKind.Number || !property.Value.TryGetInt32(out int schema) || schema < 1:
                        error.WriteLine($"`schema` in `{FileName}` has to be a whole number, like {Schema}.");
                        return null;

                    case "schema" when property.Value.GetInt32() > Schema:
                        error.WriteLine($"This project's `{FileName}` was written by a newer version of Pibbles (schema {property.Value.GetInt32()}; this version reads up to {Schema}). Update Pibbles to work on it.");
                        return null;

                    case "schema":
                        break;

                    case "story" when property.Value.ValueKind is JsonValueKind.String && property.Value.GetString() is { Length: > 0 } story:
                        settings = settings with { Story = story };
                        break;

                    case "story":
                        error.WriteLine($"`story` in `{FileName}` has to be a folder name, in quotes.");
                        return null;

                    default:
                        error.WriteLine($"I don't know the setting `{property.Name}` in `{FileName}`. The settings are {string.Join(" and ", Keys.Select(key => $"`{key}`"))}.");
                        return null;
                }
            }

            return settings;
        }
    }

    /// <summary>The settings as <c>pibbles.json</c> text, with the current schema.</summary>
    public string ToJson() => JsonText.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schema", Schema);
        writer.WriteString("story", Story);
        writer.WriteEndObject();
    }) + "\n";
}
