using CsCheck;
using Pibbles.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Random edits to the files of the <see cref="Corpus"/>: a real file with one to six mutations, two files crossed over,
/// or a whole story of several files with the mutations spread across them.
/// </summary>
internal static class Mutations
{
    private static readonly string[] Punctuation = ["@", "->", "==", "-", "[", "]", "{", "}", "$", "#", "\\", ":", "(", "\"", " ", "    ", "\t", "\n"];

    private static readonly IReadOnlyList<SourceText[]> Stories =
    [
        .. Corpus.Files
            .Where(file => file.Path.StartsWith("samples", StringComparison.Ordinal) || file.Path.Contains("Starter", StringComparison.Ordinal))
            .GroupBy(file => Path.GetDirectoryName(file.Path))
            .Select(story => story.ToArray()),
    ];

    private static readonly Gen<Mutation> AnyMutation = Gen.Select(
        Gen.OneOfConst(Enum.GetValues<MutationKind>()),
        Gen.Int[0, int.MaxValue],
        Gen.OneOfConst(Punctuation),
        (kind, position, text) => new Mutation(kind, position, text));

    public static Gen<(string Name, string Text)> MutatedFile { get; } = Gen.Select(
        Gen.Int[0, Corpus.Files.Count - 1],
        AnyMutation.Array[1, 6],
        (index, mutations) => (Corpus.Files[index].Path, mutations.Aggregate(Corpus.Files[index].Text, (text, mutation) => mutation.Apply(text))));

    /// <summary>The head of one corpus file joined to the tail of another, cut at random lines.</summary>
    public static Gen<(string Name, string Text)> CrossedFile { get; } = Gen.Select(
        Gen.Int[0, Corpus.Files.Count - 1],
        Gen.Int[0, Corpus.Files.Count - 1],
        Gen.Int[0, int.MaxValue],
        Gen.Int[0, int.MaxValue],
        (first, second, head, tail) =>
        {
            string[] headLines = Corpus.Files[first].Text.Split('\n');
            string[] tailLines = Corpus.Files[second].Text.Split('\n');
            return ($"{Corpus.Files[first].Path} + {Corpus.Files[second].Path}", string.Join('\n', [.. headLines[..(head % (headLines.Length + 1))], .. tailLines[(tail % (tailLines.Length + 1))..]]));
        });

    /// <summary>One of the corpus's stories (the files of a folder under <c>samples</c> or the starter), mutated.</summary>
    public static Gen<SourceText[]> MutatedStory { get; } = Gen.Select(
        Gen.Int[0, Stories.Count - 1],
        AnyMutation.Array[0, 6],
        (index, mutations) =>
        {
            SourceText[] files = [.. Stories[index]];
            foreach (Mutation mutation in mutations)
            {
                int target = mutation.Position % files.Length;
                files[target] = new SourceText(files[target].Path, mutation.Apply(files[target].Text));
            }

            return files;
        });

    private enum MutationKind
    {
        DeleteCharacter,
        DuplicateCharacter,
        SwapCharacters,
        ReplaceCharacter,
        Insert,
        DeleteLine,
        DuplicateLine,
        SwapLines,
        ReplaceLine,
    }

    /// <summary>One edit to a file. <see cref="Position"/> is reduced to fit the file, so any value is valid.</summary>
    private sealed record Mutation(MutationKind Kind, int Position, string Text)
    {
        public string Apply(string text)
        {
            int at = Position % (text.Length + 1);
            int character = text.Length == 0 ? 0 : Position % text.Length;
            string[] lines = text.Split('\n');
            int line = Position % lines.Length;

            return Kind switch
            {
                MutationKind.DeleteCharacter when text.Length > 0 => text.Remove(character, 1),
                MutationKind.DuplicateCharacter when text.Length > 0 => text.Insert(character, text[character].ToString()),
                MutationKind.SwapCharacters when character + 1 < text.Length => $"{text[..character]}{text[character + 1]}{text[character]}{text[(character + 2)..]}",
                MutationKind.ReplaceCharacter when text.Length > 0 => text.Remove(character, 1).Insert(character, Text),
                MutationKind.Insert => text.Insert(at, Text),
                MutationKind.DeleteLine => string.Join('\n', lines.Where((_, i) => i != line)),
                MutationKind.DuplicateLine => string.Join('\n', lines.SelectMany((content, i) => i == line ? [content, content] : new[] { content })),
                MutationKind.SwapLines when line + 1 < lines.Length => string.Join('\n', [.. lines[..line], lines[line + 1], lines[line], .. lines[(line + 2)..]]),
                MutationKind.ReplaceLine => string.Join('\n', lines.Select((content, i) => i == line ? Text : content)),
                _ => text,
            };
        }
    }
}
