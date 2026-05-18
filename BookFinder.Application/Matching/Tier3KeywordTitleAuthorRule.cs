using BookFinder.Application.Models;
using BookFinder.Application.Normalization;
using F23.StringSimilarity;

namespace BookFinder.Application.Matching;

/// <summary>
/// Backstop for cases where the LLM populated keywords instead of title.
/// Fires when hypothesis.Title is null but at least one keyword fuzzy-matches
/// the work title AND the author matches — treating keyword evidence as a
/// near-title signal rather than discarding it entirely.
/// Sits between Tier 3 and Tier 4 in the registration order.
/// </summary>
public sealed class Tier3KeywordTitleAuthorRule : IMatchRule
{
    private const double JaroWinklerThreshold = 0.82;
    private static readonly JaroWinkler JaroWinkler = new();

    private readonly TextNormalizer _normalizer;

    public Tier3KeywordTitleAuthorRule(TextNormalizer normalizer)
        => _normalizer = normalizer;

    // Shares the Tier3 enum value — ranked equally with the title-based near-match rule;
    // the hierarchy breaks ties by order of registration (title-based rule is registered first).
    public MatchTier Tier => MatchTier.Tier3NearMatchTitleAuthor;

    public MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis)
    {
        // Only run when the LLM did not produce a title but did produce keywords
        if (hypothesis.Title is not null) return null;
        if (hypothesis.Keywords.Length == 0) return null;

        var normWorkTitle = _normalizer.NormalizeTitle(work.Title);

        // Find the best-scoring keyword against the work title
        double bestScore = 0;
        foreach (var keyword in hypothesis.Keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword)) continue;
            var normKeyword = _normalizer.NormalizeTitle(keyword);
            var score = JaroWinkler.Similarity(normKeyword, normWorkTitle);
            if (score > bestScore) bestScore = score;
        }

        if (bestScore < JaroWinklerThreshold) return null;

        // Require at least a partial author match to avoid false positives
        if (hypothesis.Author is null) return null;

        var normHypAuthor = _normalizer.NormalizeAuthor(hypothesis.Author);
        var hasAuthorMatch = work.Authors.Any(a =>
            _normalizer.NormalizeAuthor(a.Name).Contains(normHypAuthor) ||
            normHypAuthor.Contains(_normalizer.NormalizeAuthor(a.Name)));

        if (!hasAuthorMatch) return null;

        return new MatchDetails(Tier, bestScore, false, null);
    }
}
