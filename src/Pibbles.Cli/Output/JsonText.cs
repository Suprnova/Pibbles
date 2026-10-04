using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;

namespace Pibbles.Cli.Output;

/// <summary>
/// Writes JSON field by field, which needs no reflection, so the trimmed CLI can use it. The output is indented with
/// <c>\n</c> line breaks, and keeps characters such as backticks readable: it's printed or saved, never embedded in HTML.
/// </summary>
internal static class JsonText
{
    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
            write(writer);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
