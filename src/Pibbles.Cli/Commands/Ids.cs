using System.Text;
using Pibbles.Cli.Projects;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Cli.Commands;

/// <summary>
/// <c>pibbles ids [root]</c>: gives every line in the story that needs a line ID and doesn't have a valid one a new one, in
/// place, touching only the ends of those lines and the invalid <c>#id</c> tags.
/// </summary>
internal static class Ids
{
    /// <summary>Adds the missing IDs, replaces the invalid ones and writes the files that change. Files the parser can't read cleanly are left alone and listed.</summary>
    /// <returns><see cref="Check.Passed"/>, <see cref="Check.Failed"/> if a file was left alone, or <see cref="Check.CouldNotRun"/>.</returns>
    public static int Run(string root, string currentDirectory, Random random, TextWriter output, TextWriter error)
    {
        if (StoryFolder.Load(root, currentDirectory, error) is not { } sources)
            return Check.CouldNotRun;

        var compilation = Compilation.Create(sources);
        IGrouping<string, TextEdit>[] files = [.. LineIds.Assign(compilation, random).GroupBy(edit => edit.Path)];
        foreach (IGrouping<string, TextEdit> file in files)
        {
            SourceText source = sources.First(source => source.Path == file.Key);
            Write(Path.GetFullPath(file.Key, currentDirectory), TextEdit.Apply(source.Text, file));
        }

        TextEdit[] replaced = [.. files.SelectMany(file => file).Where(edit => edit.Length > 0)];
        int added = files.Sum(file => file.Count()) - replaced.Length;
        output.WriteLine(added + replaced.Length == 0
            ? "Every line already has an ID."
            : $"Added {Count(added, "ID")}{(replaced.Length > 0 ? $" and replaced {Count(replaced.Length, "ID")}" : "")} in {Count(files.Length, "file")}.");

        if (replaced.Length > 0)
        {
            output.WriteLine("Anything that names a replaced ID, such as a recording or a `#voice:` tag, needs the new one:");
            foreach (TextEdit edit in replaced)
            {
                SourceText source = sources.First(source => source.Path == edit.Path);
                output.WriteLine($"  {edit.Path}:{source.GetLinePosition(edit.Start).Line + 1}  {source.Text.Substring(edit.Start, edit.Length)} -> {edit.Text}");
            }
        }

        string[] skipped = [.. compilation.SyntaxTrees.Where(tree => !LineIds.CanEdit(tree)).Select(tree => tree.Source.Path)];
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
        ILookup<string, TextEdit> edits = LineIds.Assign(compilation, random).ToLookup(edit => edit.Path);
        return [.. files.Select(file => (file.File, TextEdit.Apply(file.Text, edits[file.File])))];
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
