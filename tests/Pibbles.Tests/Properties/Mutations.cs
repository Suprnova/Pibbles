using CsCheck;

namespace Pibbles.Tests.Properties;

/// <summary>Random edits to the files of the <see cref="Corpus"/>: a real file with one to six mutations.</summary>
internal static class Mutations
{
    private static readonly string[] Punctuation = ["@", "->", "==", "-", "[", "]", "{", "}", "$", "#", "\\", ":", "(", "\"", " ", "    ", "\t", "\n"];

    private static readonly Gen<Mutation> AnyMutation = Gen.Select(
        Gen.OneOfConst(Enum.GetValues<MutationKind>()),
        Gen.Int[0, int.MaxValue],
        Gen.OneOfConst(Punctuation),
        (kind, position, text) => new Mutation(kind, position, text));

    public static Gen<(string Name, string Text)> MutatedFile { get; } = Gen.Select(
        Gen.Int[0, Corpus.Files.Count - 1],
        AnyMutation.Array[1, 6],
        (index, mutations) => (Corpus.Files[index].Path, mutations.Aggregate(Corpus.Files[index].Text, (text, mutation) => mutation.Apply(text))));

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
