using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;
using Pibbles.Syntax;
using Pibbles.Tests.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Line independence: replacing one line of a valid file with garbage at the same indentation leaves the parse of every
/// other line as it was. A line's parse is the diagnostics that start on it, and the nodes that start on it: each
/// node's kind, column and values, and the kinds of the nodes above it. An actor's display name belongs to its
/// <c>name:</c> line.
/// </summary>
/// <remarks>
/// Only a line with no deeper-indented line under it is replaced, since replacing a block's opener changes its block by
/// design. Garbage never starts with a marker that changes structure: <c>==</c>, <c>-></c>, <c>- </c>, <c>@elif</c> or
/// <c>@else</c>, or <c>//</c>. A choice is left out of a line's parse, since it starts on its first option's line.
/// </remarks>
public partial class LineIndependenceTests
{
    private static readonly Gen<string> Garbage = Gen.Select(
            Gen.OneOfConst("", "", "@if ", "@once", "@cycle", "@set ", "@jump ", "@actor ", "@var ", "@prefix ", "mira: ", "{", "[", "-x"),
            Gen.String[Gen.Char["@-=>[]{}$#\\:(),.\"'/%*+<!?_ abeilsxyz019é́"], 0, 16],
            (start, rest) => start + rest)
        .Where(IsGarbage);

    private static readonly Gen<string> ValidFile = Gen.OneOf(
        SyntaxGenerators.File.Select(SyntaxPrinter.Print),
        Gen.OneOfConst([.. Corpus.CleanFiles.Select(file => file.Text)]));

    private static readonly Gen<Case> AnyCase = ValidFile
        .Select(text => (Text: text, Lines: ReplaceableLines(text)))
        .Where(file => file.Lines.Length > 0)
        .SelectMany(file => Gen.Select(Gen.OneOfConst(file.Lines), Garbage, (line, garbage) => new Case(file.Text, line, garbage)));

    [Fact]
    public void Parse_LineReplacedWithGarbage_LeavesOtherLinesAlone() => PropertyCheck.Run(AnyCase, @case =>
    {
        var original = new SourceText("original.pib", @case.Text);
        var changed = new SourceText("changed.pib", @case.Changed);

        ILookup<int, string> before = LineParses(SyntaxTree.Parse(original));
        ILookup<int, string> after = LineParses(SyntaxTree.Parse(changed));

        string[] differences = [.. Enumerable.Range(0, original.LineCount)
            .Where(line => line != @case.Line && !before[line].SequenceEqual(after[line]))
            .Select(line => $"line {line + 1}: [{string.Join("; ", before[line])}] became [{string.Join("; ", after[line])}]")];
        Assert.Empty(differences);
    }, iterations: 1000, print: @case => @case.ToString());

    private static bool IsGarbage(string garbage) =>
        garbage.Length > 0
        && garbage[0] is not (' ' or '\t')
        && !garbage.StartsWith("==", StringComparison.Ordinal)
        && !garbage.StartsWith("->", StringComparison.Ordinal)
        && !garbage.StartsWith("//", StringComparison.Ordinal)
        && garbage is not ("-" or ['-', ' ' or '\t', ..])
        && !ElseClause().IsMatch(garbage);

    /// <summary>The lines that hold content, aren't headers, and have no deeper-indented line directly under them.</summary>
    private static int[] ReplaceableLines(string text)
    {
        var source = new SourceText("file.pib", text);
        (int Line, int Width, LineKind Kind)[] lines = [.. Enumerable.Range(0, source.LineCount)
            .Select(line => (Line: line, Text: LineText(source, line)))
            .Select(line => (line.Line, Width: IndentationWidth(line.Text), Kind: LineClassifier.KindOf(line.Text.AsSpan(IndentationWidth(line.Text)))))
            .Where(line => line.Kind is not (LineKind.Blank or LineKind.Comment or LineKind.Note))];

        return [.. lines
            .Where((line, i) => line.Kind is not LineKind.Header && (i + 1 == lines.Length || lines[i + 1].Width <= line.Width))
            .Select(line => line.Line)];
    }

    private static string LineText(SourceText source, int line)
    {
        TextSpan span = source.GetLineSpan(line);
        return source.Text.Substring(span.Start, span.Length);
    }

    private static int IndentationWidth(string line) => line.Length - line.TrimStart(" \t").Length;

    private static ILookup<int, string> LineParses(SyntaxTree tree)
    {
        List<(int Line, string Parse)> parses = [];
        Visit(tree.Root, "");
        parses.AddRange(tree.Diagnostics.Select(diagnostic => (diagnostic.Location.Start.Line, $"{diagnostic.Code}@{diagnostic.Location.Start.Column} {diagnostic.Message}")));
        return parses.ToLookup(parse => parse.Line, parse => parse.Parse);

        void Visit(SyntaxNode node, string path)
        {
            string kind = node.GetType().Name;
            if (node is not (FileSyntax or ChoiceSyntax))
            {
                LinePosition start = tree.Source.GetLinePosition(node.Span.Start);
                parses.Add((start.Line, $"{path}/{kind}@{start.Column}{Values(node)}"));
            }

            if (node is ActorDeclarationSyntax { DisplayNameSpan: { } displayName } actor)
                parses.Add((tree.Source.GetLinePosition(displayName.Start).Line, $"{path}/{kind} name: {actor.DisplayName}"));

            foreach (SyntaxNode child in NodeFields.Children(node))
                Visit(child, $"{path}/{kind}");
        }
    }

    /// <summary>A node's values, apart from its spans and an actor's display name, which belongs to its <c>name:</c> line.</summary>
    private static string Values(SyntaxNode node) => string.Concat(NodeFields.Of(node.GetType())
        .Where(field => !NodeFields.HoldsChildren(field) && field.PropertyType != typeof(TextSpan) && field.PropertyType != typeof(TextSpan?)
            && field.Name is not nameof(ActorDeclarationSyntax.DisplayName))
        .Select(field => $" {field.Name}={Convert.ToString(field.GetValue(node), CultureInfo.InvariantCulture)}"));

    [GeneratedRegex(@"^@(elif|else)(?![\p{L}\p{Nd}_])")]
    private static partial Regex ElseClause();

    private sealed record Case(string Text, int Line, string Garbage)
    {
        public string Changed
        {
            get
            {
                var source = new SourceText("file.pib", Text);
                TextSpan line = source.GetLineSpan(Line);
                string original = LineText(source, Line);
                return Text[..line.Start] + original[..IndentationWidth(original)] + Garbage + Text[line.End..];
            }
        }

        public override string ToString() => $"Line {Line + 1} replaced with `{Garbage}`:\n{Changed}";
    }
}
