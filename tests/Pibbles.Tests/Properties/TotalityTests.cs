using CsCheck;
using Pibbles.Compiler;
using Pibbles.Semantics;
using Pibbles.Syntax;
using Pibbles.Tests.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Totality: any input compiles without throwing, into a tree whose spans lie inside the file, with each node's children
/// inside it and in order. The input is random lines, or a real file with random mutations.
/// </summary>
public class TotalityTests
{
    private const string Alphabet = "@-=>[]{}$#\\:(),.\"'/%*+<!?_ abxyz019\té́﻿\uD83D";

    private static readonly string[] Punctuation = ["@", "->", "==", "-", "[", "]", "{", "}", "$", "#", "\\", ":", "(", "\"", " ", "    ", "\t", "\n"];

    private static readonly Gen<string> RandomLine = Gen.Select(
        Gen.OneOfConst("", "", "    ", "        ", "\t", "  ", " \t"),
        Gen.OneOfConst("", "==", "@", "->", "- ", "-", "//", "///", "@if ", "@elif ", "@else", "@once", "@cycle", "@sequence", "@actor ", "@var ",
            "@command ", "@prefix ", "@set ", "@jump ", "@call ", "mira: ", "mira (sad):", "name: ", "poses: ", "{", "[", "#"),
        Gen.String[Gen.Char[Alphabet], 0, 16],
        (indent, start, rest) => indent + start + rest);

    private static readonly Gen<string> RandomInput = Gen.OneOf(
        Gen.Select(RandomLine.Array[0, 12], Gen.OneOfConst("\n", "\r\n", "\r"), (lines, newline) => string.Join(newline, lines)),
        Gen.String[Gen.Char[Alphabet + "\n\r"], 0, 80]);

    private static readonly Gen<Mutation> AnyMutation = Gen.Select(
        Gen.OneOfConst(Enum.GetValues<MutationKind>()),
        Gen.Int[0, int.MaxValue],
        Gen.OneOfConst(Punctuation),
        (kind, position, text) => new Mutation(kind, position, text));

    private static readonly Gen<(string Name, string Text)> MutatedFile = Gen.Select(
        Gen.Int[0, Corpus.Files.Count - 1],
        AnyMutation.Array[1, 6],
        (index, mutations) => (Corpus.Files[index].Path, mutations.Aggregate(Corpus.Files[index].Text, (text, mutation) => mutation.Apply(text))));

    [Fact]
    public void Compile_RandomInput_IsTotal() =>
        PropertyCheck.Run(RandomInput, AssertTotal, iterations: 3000, print: Visible);

    [Fact]
    public void Compile_MutatedFile_IsTotal() =>
        PropertyCheck.Run(MutatedFile, file => AssertTotal(file.Text), iterations: 3000, print: file => $"{file.Name}, mutated:\n{Visible(file.Text)}");

    [Fact]
    public void Compile_CorpusFileWithoutErrors_LowersWithoutThrowing() =>
        Assert.All(Corpus.Files, file => AssertLowers(file.Text));

    [Fact]
    public void Compile_MutatedFileWithoutErrors_LowersWithoutThrowing() =>
        PropertyCheck.Run(MutatedFile, file => AssertLowers(file.Text), iterations: 3000, print: file => $"{file.Name}, mutated:\n{Visible(file.Text)}");

    /// <summary>Compiling lowers a story with no errors, and a name the binder left unresolved there would throw.</summary>
    private static void AssertLowers(string text)
    {
        CompileResult result = StoryCompiler.Compile([new SourceText("input.pib", text)]);

        Assert.Equal(result.Story is null, result.HasErrors);
    }

    private static void AssertTotal(string text)
    {
        var source = new SourceText("input.pib", text);

        var compilation = Compilation.Create([source]);
        SyntaxTree tree = Assert.Single(compilation.SyntaxTrees);

        AssertNested(tree.Root, new TextSpan(0, text.Length));
        Assert.All(compilation.Diagnostics, diagnostic => Assert.True(Contains(tree.Root.Span, diagnostic.Location.Span), $"{diagnostic.Code} at {diagnostic.Location.Span} lies outside the file."));
    }

    /// <summary>Checks that <paramref name="node"/> lies inside <paramref name="parent"/>, and that its children lie inside it, in order, without overlapping.</summary>
    private static void AssertNested(SyntaxNode node, TextSpan parent)
    {
        Assert.True(Contains(parent, node.Span), $"{node.GetType().Name} at {node.Span} lies outside its parent at {parent}.");

        int previousEnd = node.Span.Start;
        foreach (SyntaxNode child in NodeFields.Children(node))
        {
            Assert.True(child.Span.Start >= previousEnd, $"{child.GetType().Name} at {child.Span} starts before the end of the node before it, at {previousEnd}.");
            AssertNested(child, node.Span);
            previousEnd = child.Span.End;
        }
    }

    private static bool Contains(TextSpan outer, TextSpan inner) => inner.Start >= outer.Start && inner.End <= outer.End;

    /// <summary>Shows line breaks and tabs, which a failure message would otherwise hide.</summary>
    private static string Visible(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

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
