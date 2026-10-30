using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>A change to a source file: the text at a position is replaced, or, with no length, text is inserted.</summary>
/// <param name="Path">The file's path, as given in its <see cref="SourceText"/>.</param>
/// <param name="Start">Where the change starts, as a character position in the file.</param>
/// <param name="Length">How many characters it replaces, or 0 to insert.</param>
/// <param name="Text">The text that goes in.</param>
public readonly record struct TextEdit(string Path, int Start, int Length, string Text)
{
    /// <summary>Makes every edit for one file in its text. Positions refer to the text before any edit.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="edits">The edits for that file, which don't overlap.</param>
    public static string Apply(string text, IEnumerable<TextEdit> edits) =>
        edits.OrderByDescending(edit => edit.Start).Aggregate(text, (result, edit) => result.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text));
}

/// <summary>
/// Makes line IDs for the lines that need one: text lines that show text, options, <c>@call</c>, and variation block
/// openers that have none, or whose ID isn't a valid line ID. This is what <c>pibbles ids</c> does.
/// </summary>
public static class LineIds
{
    private const string Letters = "abcdefghijklmnopqrstuvwxyz";
    private const string LettersAndDigits = Letters + "0123456789";

    /// <summary>
    /// Whether <see cref="Assign"/> can edit a file: its only problems, if any, are IDs that aren't valid line IDs. Any other
    /// syntax error could mean a misread line, whose ID would end up in the wrong place.
    /// </summary>
    /// <param name="tree">The file's syntax tree.</param>
    public static bool CanEdit(SyntaxTree tree) => tree.Diagnostics.All(diagnostic => diagnostic.Descriptor == DiagnosticCatalog.InvalidLineId);

    /// <summary>
    /// Finds every line that needs an ID, and makes a new one for each. A line with none gets <c> #id:k7qp2x</c> at the end
    /// of the line, after its tags and before a trailing comment; a line whose ID isn't a valid line ID gets its
    /// <c>#id</c> tag replaced. A comment that touches the end of the line or the replaced tag gets a space before it. Files that <see cref="CanEdit"/> says no to are left out.
    /// </summary>
    /// <param name="compilation">The story.</param>
    /// <param name="random">Where new IDs come from. A new ID never matches an ID, node name or old node name already in the story.</param>
    /// <returns>One edit for each missing or invalid ID, in the order the files were given. A replacement has a length.</returns>
    public static IReadOnlyList<TextEdit> Assign(Compilation compilation, Random random)
    {
        HashSet<string> taken =
        [
            .. compilation.SyntaxTrees.SelectMany(LineIdSites.In).Select(site => site.Id?.Value).OfType<string>(),
            .. compilation.Symbols.Nodes.Keys,
            .. compilation.Symbols.Aliases.Keys,
        ];

        return
        [
            .. compilation.SyntaxTrees.Where(CanEdit).SelectMany(tree =>
            {
                HashSet<TextSpan> invalid = [.. tree.Diagnostics.Select(diagnostic => diagnostic.Location.Span)];
                return LineIdSites.In(tree).Select(site => site switch
                {
                    { Id: null } => new TextEdit(tree.Source.Path, site.Insert, 0, $" #id:{NewId(random, taken)}{Gap(tree.Source, site.Insert)}"),
                    { Id: { } id } when invalid.Contains(id.Span) => new TextEdit(tree.Source.Path, id.Span.Start, id.Span.Length, $"#id:{NewId(random, taken)}{Gap(tree.Source, id.Span.End)}"),
                    _ => (TextEdit?)null,
                }).OfType<TextEdit>();
            }),
        ];
    }

    /// <summary>A space when a comment starts at <paramref name="position"/>, which a tag's value would otherwise swallow.</summary>
    private static string Gap(SourceText source, int position) => source.Text.AsSpan(position).StartsWith("//") ? " " : "";

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
