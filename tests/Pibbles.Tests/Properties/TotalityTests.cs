using CsCheck;
using Pibbles.Compiler;
using Pibbles.Configuration;
using Pibbles.Semantics;
using Pibbles.Syntax;
using Pibbles.Tests.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Totality: any input compiles without throwing, into a tree whose spans lie inside the file, with each node's children
/// inside it and in order, and every position answers a semantic lookup. The input is random lines, a real file with random
/// mutations, two files crossed over, or a whole story of several files, under default or the strictest settings.
/// </summary>
public class TotalityTests
{
    private const string Alphabet = "@-=>[]{}$#\\:(),.\"'/%*+<!?_ abxyz019\té́﻿\0\v\f\u0085\u2028\u200D\uDC00\uD83D：（）、，" + Spaces.Ideographic + Spaces.NoBreak;

    /// <summary>Words and markers the grammar reads, with the full-width forms an input method types in their place.</summary>
    private static readonly string[] Words =
    [
        "if", "elif", "else", "{if", "{elif", "{else}", "{/if}", "true", "false", "and", "or", "not", "not(", "and(", "or(", "was", "id", "inline", "waits", "wait", "nowait",
        "{w}", "{br}", "{page}", "{pause", "{speed", "{icon", "{@", "[b]", "[/b]", "[color=", "1s", "0.5s", "12", "-3", "$x", "$", "x", "x.y", "f(", "#a", "#id:", "#was:", "@once", "@if",
        "＠", "＃", "＄", "＄x", "＝", "＝＝", "＞", "ー", "ー＞", "「", "」", "【", "】", "［", "］", "｛", "｝", "『", "』", "・", "、", "￥", "：", "（", "）", "！＝",
        "\\", "//", "\r", "\n", " ", "  ", "\t",
    ];

    /// <summary>Settings that put every style threshold at 1, so every rule that counts something has its limit crossed.</summary>
    private static readonly FileSettings StrictSettings = FileSettings.From(
        new[] { "pibbles_max_nesting", "pibbles_max_option_body", "pibbles_min_repeated_lines", "pibbles_min_repeated_colors", "pibbles_max_message_length", "pibbles_max_option_length" }
            .Select(name => KeyValuePair.Create(name, "1")));

    private static readonly Gen<string> Chunk = Gen.Frequency((4, Gen.OneOfConst(Words)), (1, Gen.String[Gen.Char[Alphabet], 1, 4]));

    private static readonly Gen<string> RandomLine = Gen.Select(
        Gen.OneOfConst("", "", "    ", "        ", "\t", "  ", " \t"),
        Gen.OneOfConst("", "==", "@", "->", "- ", "-", "//", "///", "@if ", "@elif ", "@else", "@once", "@cycle", "@sequence", "@actor ", "@var ",
            "@command ", "@prefix ", "@set ", "@jump ", "@call ", "mira: ", "mira (sad):", "name: ", "pose: ", "pose += ", "no ", "@channel ", "@narration ", "(sad):", "mira (sad, still):", "{", "[", "#",
            "＠", "＠if ", "ー", "ー＞ ", "＝＝ ", "mira： ", "（sad）：", "mira （sad）："),
        Gen.OneOf(Chunk.Array[0, 6].Select(string.Concat), Chunk.Array[0, 6].Select(chunks => string.Join(' ', chunks)), Gen.String[Gen.Char[Alphabet], 0, 16]),
        (indent, start, rest) => indent + start + rest);

    private static readonly Gen<string> RandomInput = Gen.OneOf(
        Gen.Select(RandomLine.Array[0, 12], Gen.OneOfConst("\n", "\r\n", "\r"), (lines, newline) => string.Join(newline, lines)),
        Gen.String[Gen.Char[Alphabet + "\n\r"], 0, 80]);

    [Fact]
    public void Compile_RandomInput_IsTotal() =>
        PropertyCheck.Run(RandomInput, AssertTotal, print: Visible);

    [Fact]
    public void Compile_MutatedFile_IsTotal() =>
        PropertyCheck.Run(Mutations.MutatedFile, file => AssertTotal(file.Text), print: file => $"{file.Name}, mutated:\n{Visible(file.Text)}");

    [Fact]
    public void Compile_MutatedFileWithEveryThresholdAtOne_IsTotal() =>
        PropertyCheck.Run(Mutations.MutatedFile, file => AssertTotal([new SourceText("input.pib", file.Text)], Strict(["input.pib"])), print: file => $"{file.Name}, mutated:\n{Visible(file.Text)}");

    [Fact]
    public void Compile_CrossedFiles_IsTotal() =>
        PropertyCheck.Run(Mutations.CrossedFile, file => AssertTotal(file.Text), print: file => $"{file.Name}:\n{Visible(file.Text)}");

    [Fact]
    public void Compile_MutatedStory_IsTotal() =>
        PropertyCheck.Run(Gen.Select(Mutations.MutatedStory, Gen.Bool), input => AssertTotal(input.Item1, input.Item2 ? Strict(input.Item1.Select(file => file.Path)) : null),
            print: input => string.Join("\n", input.Item1.Select(file => $"{file.Path}{(input.Item2 ? " (strict)" : "")}:\n{Visible(file.Text)}")));

    [Fact]
    public void Compile_CorpusFileWithoutErrors_LowersWithoutThrowing() =>
        Assert.All(Corpus.Files, file => AssertLowers(file.Text));

    [Fact]
    public void Compile_MutatedFileWithoutErrors_LowersWithoutThrowing() =>
        PropertyCheck.Run(Mutations.MutatedFile, file => AssertLowers(file.Text), print: file => $"{file.Name}, mutated:\n{Visible(file.Text)}");

    /// <summary>Compiling lowers a story with no errors, and a name the binder left unresolved there would throw.</summary>
    private static void AssertLowers(string text)
    {
        CompileResult result = StoryCompiler.Compile([new SourceText("input.pib", text)]);

        Assert.Equal(result.Story is null, result.HasErrors);
    }

    private static void AssertTotal(string text) => AssertTotal([new SourceText("input.pib", text)]);

    private static void AssertTotal(SourceText[] sources, CompilationOptions? options = null)
    {
        var compilation = Compilation.Create(sources, options);

        Assert.Equal(sources.Length, compilation.SyntaxTrees.Count);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            AssertNested(tree.Root, new TextSpan(0, tree.Source.Text.Length));
            AssertLookups(compilation, tree.Source);
        }

        Assert.All(compilation.Diagnostics, diagnostic =>
        {
            SyntaxTree tree = compilation.SyntaxTrees.First(tree => tree.Source.Path == diagnostic.Location.Path);
            Assert.True(Contains(tree.Root.Span, diagnostic.Location.Span), $"{diagnostic.Code} at {diagnostic.Location.Span} lies outside {diagnostic.Location.Path}.");
        });
    }

    /// <summary>Asks the model for the symbol at every position of a file, and for every place each one is used.</summary>
    private static void AssertLookups(Compilation compilation, SourceText source)
    {
        for (int position = 0; position <= source.Text.Length; position++)
        {
            if (compilation.Model.GetSymbolAt(source.Path, position) is { } symbol)
                Assert.NotNull(compilation.Model.FindReferences(symbol));
        }
    }

    private static CompilationOptions Strict(IEnumerable<string> paths) => new(paths.Distinct().ToDictionary(path => path, _ => StrictSettings));

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
}
