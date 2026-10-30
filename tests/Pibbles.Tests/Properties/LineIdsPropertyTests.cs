using Pibbles.Semantics;
using Pibbles.Syntax;
using Pibbles.Tests.Snapshots;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Adding line IDs: for a file <see cref="LineIds.CanEdit"/> accepts, the edits change the tree only by its <c>#id</c>
/// tags, keep every comment, leave no diagnostic, and a second pass makes no edits.
/// </summary>
public class LineIdsPropertyTests
{
    [Fact]
    public void Assign_MutatedFile_ChangesNothingButIds() => PropertyCheck.Run(Mutations.MutatedFile, file =>
    {
        var source = new SourceText("input.pib", file.Text);
        var compilation = Compilation.Create([source]);
        SyntaxTree before = Assert.Single(compilation.SyntaxTrees);
        if (!LineIds.CanEdit(before))
            return;

        var edited = new SourceText(source.Path, TextEdit.Apply(file.Text, LineIds.Assign(compilation, new Random(0))));
        var recompiled = Compilation.Create([edited]);
        SyntaxTree after = Assert.Single(recompiled.SyntaxTrees);

        Assert.Equal(WithoutIds(before), WithoutIds(after));
        Assert.Equal(before.Comments.Select(comment => comment.Text), after.Comments.Select(comment => comment.Text));
        Assert.Empty(after.Diagnostics);
        Assert.Empty(LineIds.Assign(recompiled, new Random(0)));
    }, iterations: 3000, print: file => $"{file.Name}, mutated:\n{file.Text}");

    /// <summary>The tree's shape without its <c>#id</c> tags, or the empty tag lists they leave.</summary>
    private static string WithoutIds(SyntaxTree tree) => string.Join('\n', SyntaxDump.WriteShape(tree.Root)
        .Split('\n')
        .Select(line => line.TrimStart())
        .Where(line => line is not "Tags:" && !line.StartsWith("Tag Name=\"id\"", StringComparison.Ordinal)));
}
