using System.Text;
using Pibbles.Cli.Projects;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Cli.Commands;

/// <summary>
/// <c>pibbles ids [root]</c>: adds a line ID to every line in the story that needs one and doesn't have it, in place,
/// touching only the ends of those lines.
/// </summary>
internal static class Ids
{
    /// <summary>Adds the missing IDs and writes the files that change. Files the parser can't read cleanly are left alone and listed.</summary>
    /// <returns><see cref="Check.Passed"/>, <see cref="Check.Failed"/> if a file was left alone, or <see cref="Check.CouldNotRun"/>.</returns>
    public static int Run(string root, string currentDirectory, Random random, TextWriter output, TextWriter error)
    {
        if (StoryFolder.Load(root, currentDirectory, error) is not { } sources)
            return Check.CouldNotRun;

        var compilation = Compilation.Create(sources);
        IGrouping<string, TextInsertion>[] files = [.. LineIds.AddMissing(compilation, random).GroupBy(insertion => insertion.Path)];
        foreach (IGrouping<string, TextInsertion> file in files)
        {
            SourceText source = sources.First(source => source.Path == file.Key);
            Write(Path.GetFullPath(file.Key, currentDirectory), TextInsertion.Apply(source.Text, file));
        }

        int added = files.Sum(file => file.Count());
        output.WriteLine(added == 0
            ? "Every line already has an ID."
            : $"Added {Count(added, "ID")} in {Count(files.Length, "file")}.");

        string[] skipped = [.. compilation.SyntaxTrees.Where(tree => tree.Diagnostics.Count > 0).Select(tree => tree.Source.Path)];
        if (skipped.Length == 0)
            return Check.Passed;

        error.WriteLine("I didn't add IDs to these files, because some of their lines have mistakes that could put an ID in the wrong place. Run `pibbles check` to find them, then run `pibbles ids` again:");
        foreach (string path in skipped)
            error.WriteLine($"  {path}");

        return Check.Failed;
    }

    /// <summary>Adds IDs to story files that are about to be written, such as the starter story, given as file names and text.</summary>
    public static IReadOnlyList<(string File, string Text)> AddTo(IReadOnlyList<(string File, string Text)> files, Random random)
    {
        var compilation = Compilation.Create(files.Select(file => new SourceText(file.File, file.Text)));
        ILookup<string, TextInsertion> insertions = LineIds.AddMissing(compilation, random).ToLookup(insertion => insertion.Path);
        return [.. files.Select(file => (file.File, TextInsertion.Apply(file.Text, insertions[file.File])))];
    }

    /// <summary>Writes a file, keeping the byte-order mark it had, if any.</summary>
    private static void Write(string path, string text)
    {
        byte[] start = new byte[3];
        using (FileStream stream = File.OpenRead(path))
            stream.ReadExactly(start, 0, Math.Min(3, (int)stream.Length));

        bool byteOrderMark = start is [0xEF, 0xBB, 0xBF];
        File.WriteAllText(path, text, new UTF8Encoding(byteOrderMark));
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
