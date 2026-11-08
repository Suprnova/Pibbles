namespace Pibbles.Semantics;

/// <summary>Lists of names as messages write them.</summary>
internal static class Phrase
{
    /// <summary>Quotes each name and joins them with a final "or".</summary>
    /// <param name="names">The names to quote, in order.</param>
    /// <returns><c>`left`, `center` or `right`</c>, or an empty string for no names.</returns>
    public static string Or(IEnumerable<string> names) => Join(names, "or");

    /// <summary>Quotes each name and joins them with a final "and".</summary>
    /// <param name="names">The names to quote, in order.</param>
    /// <returns><c>`who` and `to`</c>, or an empty string for no names.</returns>
    public static string And(IEnumerable<string> names) => Join(names, "and");

    private static string Join(IEnumerable<string> names, string conjunction) => names.Select(name => $"`{name}`").ToArray() switch
    {
        [] => "",
        [var only] => only,
        [.. var first, var last] => $"{string.Join(", ", first)} {conjunction} {last}",
    };
}
