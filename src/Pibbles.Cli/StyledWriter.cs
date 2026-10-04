using System.Text;

namespace Pibbles.Cli;

/// <summary>
/// A writer for messages to people, such as <c>init</c>'s report and the errors the commands print, that renders their
/// <see cref="Markup"/> on the way out.
/// </summary>
internal sealed class StyledWriter(TextWriter inner, bool color) : TextWriter
{
    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value) => inner.Write(value);

    public override void Write(string? value) => inner.Write(value is null ? null : Markup.Render(value, color));

    public override void WriteLine(string? value) => inner.WriteLine(value is null ? null : Markup.Render(value, color));

    public override void Flush() => inner.Flush();
}
