using BookFinder.Application.Models;

namespace BookFinder.Application.OpenLibrary;

public sealed class Deduplicator
{
    public IReadOnlyList<OpenLibraryWork> Deduplicate(IReadOnlyList<OpenLibraryWork> works)
    {
        // Group by canonical work key; keep first occurrence (highest ranked by OL relevance)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<OpenLibraryWork>(works.Count);

        foreach (var work in works)
        {
            var key = NormalizeKey(work.WorkKey);
            if (seen.Add(key))
                result.Add(work);
        }

        return result;
    }

    private static string NormalizeKey(string key)
        => key.Trim().TrimStart('/').ToLowerInvariant();
}
