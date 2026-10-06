using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Text to insert into a source file at a position.</summary>
/// <param name="Path">The file's path, as given in its <see cref="SourceText"/>.</param>
/// <param name="Position">Where the text goes, as a character position in the file.</param>
/// <param name="Text">The text to insert.</param>
public readonly record struct TextInsertion(string Path, int Position, string Text)
{
    /// <summary>Inserts every insertion for one file into its text. Positions refer to the text before any insertion.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="insertions">The insertions for that file.</param>
    public static string Apply(string text, IEnumerable<TextInsertion> insertions) =>
        insertions.OrderByDescending(insertion => insertion.Position).Aggregate(text, (result, insertion) => result.Insert(insertion.Position, insertion.Text));
}

/// <summary>
/// Makes line IDs for the lines that need one and don't have it: text lines that show text, options, <c>@call</c>, and
/// variation block openers. This is what <c>pibbles ids</c> does.
/// </summary>
public static class LineIds
{
    private const string Letters = "abcdefghijklmnopqrstuvwxyz";
    private const string LettersAndDigits = Letters + "0123456789";

    /// <summary>
    /// Finds every line that needs an ID and doesn't have one, and makes a new ID for each, written as <c> #id:k7qp2x</c>
    /// at the end of the line, after its tags and before a trailing comment. Files with syntax errors are left out,
    /// since a misread line could end in the wrong place.
    /// </summary>
    /// <param name="compilation">The story.</param>
    /// <param name="random">Where new IDs come from. A new ID never matches an ID, node name or old node name already in the story.</param>
    /// <returns>One insertion for each missing ID, in the order the files were given.</returns>
    public static IReadOnlyList<TextInsertion> AddMissing(Compilation compilation, Random random)
    {
        HashSet<string> taken =
        [
            .. compilation.SyntaxTrees.SelectMany(LineIdSites.In).Select(site => site.Id?.Value).OfType<string>(),
            .. compilation.Symbols.Nodes.Keys,
            .. compilation.Symbols.Aliases.Keys,
        ];

        return
        [
            .. compilation.SyntaxTrees
                .Where(tree => tree.Diagnostics.Count == 0)
                .SelectMany(tree => LineIdSites.In(tree).Where(site => site.Id is null).Select(site => new TextInsertion(tree.Source.Path, site.Insert, $" #id:{NewId(random, taken)}"))),
        ];
    }

    /// <summary>Makes a lowercase letter and five lowercase letters or digits, different from every taken name, and takes it.</summary>
    private static string NewId(Random random, HashSet<string> taken)
    {
        string id;
        do
            id = string.Concat(Letters[random.Next(Letters.Length)], new string([.. Enumerable.Range(0, 5).Select(_ => LettersAndDigits[random.Next(LettersAndDigits.Length)])]));
        while (!taken.Add(id));

        return id;
    }
}
