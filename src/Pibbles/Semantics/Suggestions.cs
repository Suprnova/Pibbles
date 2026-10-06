namespace Pibbles.Semantics;

/// <summary>"Did you mean" suggestions: the closest name to a misspelled one, among the names it could have meant.</summary>
internal static class Suggestions
{
    /// <summary>
    /// Finds the candidate closest to <paramref name="name"/>, ignoring case, within <c>max(1, length / 3)</c> edits.
    /// An edit inserts, deletes or replaces a letter, or swaps two letters side by side. Ties go to the earliest candidate.
    /// </summary>
    /// <returns>The closest candidate, or <see langword="null"/> if none is close enough.</returns>
    public static string? Closest(string name, IEnumerable<string> candidates)
    {
        int limit = Math.Max(1, name.Length / 3);
        return candidates
            .Select(candidate => (Candidate: candidate, Distance: Distance(name.ToLowerInvariant(), candidate.ToLowerInvariant())))
            .Where(match => match.Distance <= limit)
            .OrderBy(match => match.Distance)
            .Select(match => match.Candidate)
            .FirstOrDefault();
    }

    /// <summary>The optimal string alignment distance: Levenshtein distance, plus swaps of two letters side by side.</summary>
    private static int Distance(string a, string b)
    {
        int[,] distance = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
            distance[i, 0] = i;
        for (int j = 0; j <= b.Length; j++)
            distance[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    distance[i, j] = Math.Min(distance[i, j], distance[i - 2, j - 2] + 1);
            }
        }

        return distance[a.Length, b.Length];
    }
}
