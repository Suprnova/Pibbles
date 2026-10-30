namespace Pibbles.Tests.Syntax;

/// <summary>Spaces other than U+0020 that look alike or invisible in an editor, named so a test says which one it uses.</summary>
internal static class Spaces
{
    public const string NoBreak = "\x00A0";
    public const string NarrowNoBreak = "\x202F";
    public const string Ideographic = "\x3000";
}
