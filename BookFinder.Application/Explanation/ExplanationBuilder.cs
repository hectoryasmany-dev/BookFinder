using BookFinder.Application.Models;

namespace BookFinder.Application.Explanation;

public sealed class ExplanationBuilder
{
    public string Build(RankedCandidate candidate, ExtractedHypothesis hypothesis)
    {
        var work = candidate.Work;
        var match = candidate.Match;
        var primaryAuthor = work.Authors.FirstOrDefault(a => a.IsPrimaryAuthor)?.Name
                         ?? work.Authors.FirstOrDefault()?.Name;

        return match.Tier switch
        {
            MatchTier.Tier1ExactTitlePrimaryAuthor => BuildTier1(primaryAuthor),
            MatchTier.Tier2ExactTitleContributor   => BuildTier2(primaryAuthor, match),
            MatchTier.Tier3NearMatchTitleAuthor    => BuildTier3(primaryAuthor, match),
            MatchTier.Tier4AuthorOnly              => BuildTier4(primaryAuthor),
            _                                      => BuildTier5(work, hypothesis)
        };
    }

    private static string BuildTier1(string? primaryAuthor)
    {
        var parts = new List<string> { "Exact title match" };
        if (primaryAuthor is not null)
            parts.Add($"{primaryAuthor} is primary author");
        return string.Join("; ", parts) + ".";
    }

    private static string BuildTier2(string? primaryAuthor, MatchDetails match)
    {
        var parts = new List<string> { "Exact title match" };
        if (match.ContributorNote is not null)
            parts.Add(match.ContributorNote);
        else if (primaryAuthor is not null)
            parts.Add($"queried author is a contributor, not primary ({primaryAuthor} is primary)");
        return string.Join("; ", parts) + ".";
    }

    private static string BuildTier3(string? primaryAuthor, MatchDetails match)
    {
        var score = match.SimilarityScore.HasValue
            ? $" (Jaro-Winkler {match.SimilarityScore.Value:F2})"
            : string.Empty;
        var parts = new List<string> { $"Near-match title{score}" };
        if (primaryAuthor is not null)
            parts.Add($"{primaryAuthor} primary author");
        return string.Join("; ", parts) + ".";
    }

    private static string BuildTier4(string? primaryAuthor)
    {
        var author = primaryAuthor ?? "unknown author";
        return $"Author-only match: top work by {author}.";
    }

    private static string BuildTier5(OpenLibraryWork work, ExtractedHypothesis hypothesis)
    {
        var tokens = hypothesis.Keywords.Length > 0
            ? $"keyword match on \"{string.Join(", ", hypothesis.Keywords)}\""
            : "partial relevance";
        return $"No strong match found; included by {tokens}.";
    }
}
