namespace Pibbles.Semantics;

/// <summary>Lists of names as messages write them.</summary>
internal static class Phrase
{
    /// <summary>Quotes each name and joins them: <c>`left`, `center` or `right`</c>.</summary>
    public static string Or(IEnumerable<string> names) => Join(names, "or");

    /// <summary>Quotes each name and joins them: <c>`who` and `to`</c>.</summary>
    public static string And(IEnumerable<string> names) => Join(names, "and");

    private static string Join(IEnumerable<string> names, string conjunction) => names.Select(name => $"`{name}`").ToArray() switch
    {
        [] => "",
        [var only] => only,
        [.. var first, var last] => $"{string.Join(", ", first)} {conjunction} {last}",
    };
}
