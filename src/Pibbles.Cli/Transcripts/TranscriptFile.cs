namespace Pibbles.Cli.Transcripts;

/// <summary>
/// Transcript files on disk, and what counts as a match: replaying a file prints its text exactly, once its line endings
/// are <c>\n</c>. <c>pibbles test</c> and the repository's own transcript tests both use this, so they always agree.
/// </summary>
internal static class TranscriptFile
{
    /// <summary>Every <c>.transcript</c> file under a folder, at any depth, in ordinal order, leaving out the <c>.received.</c> files a failing test writes.</summary>
    public static IEnumerable<string> Find(string folder) =>
        Directory.EnumerateFiles(folder, "*.transcript", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Contains(".received.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    /// <summary>A transcript's text as the player writes it: with <c>\n</c> line endings.</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
