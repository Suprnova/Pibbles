using System.Text;

namespace Pibbles.Cli.Output;

/// <summary>
/// Renders the light markup in messages and explanations: <c>`code`</c> and <c>**bold**</c>. With color, code is cyan
/// and bold is bold. Without it, code keeps its backticks, the only marker left, and bold loses its asterisks.
/// </summary>
/// <remarks>Only prose goes through here. Source lines and examples are always shown exactly as written.</remarks>
internal static class Markup
{
    public static string Render(string text, bool color)
    {
        var rendered = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '`' && text.IndexOf('`', i + 1) is var close and > 0)
            {
                string code = text[(i + 1)..close];
                rendered.Append(color ? $"\e[36m{code}\e[39m" : $"`{code}`");
                i = close;
            }
            else if (text.AsSpan(i).StartsWith("**") && text.IndexOf("**", i + 2, StringComparison.Ordinal) is var end and > 0)
            {
                string bold = Render(text[(i + 2)..end], color);
                rendered.Append(color ? $"\e[1m{bold}\e[22m" : bold);
                i = end + 1;
            }
            else
            {
                rendered.Append(text[i]);
            }
        }

        return rendered.ToString();
    }
}
