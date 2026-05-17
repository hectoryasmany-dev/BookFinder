using BookFinder.Application.Models;
using BookFinder.Application.Normalization;

namespace BookFinder.Application.Matching;

public sealed class Tier1ExactTitlePrimaryAuthorRule : IMatchRule
{
    private readonly TextNormalizer _normalizer;

    public Tier1ExactTitlePrimaryAuthorRule(TextNormalizer normalizer)
        => _normalizer = normalizer;

    public MatchTier Tier => MatchTier.Tier1ExactTitlePrimaryAuthor;

    public MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis)
    {
        if (string.IsNullOrWhiteSpace(hypothesis.Title))
            return null;

        var normalizedWorkTitle = _normalizer.NormalizeTitle(work.Title);
        var normalizedHypTitle = _normalizer.NormalizeTitle(hypothesis.Title);

        if (!string.Equals(normalizedWorkTitle, normalizedHypTitle, StringComparison.Ordinal))
            return null;

        // No author in hypothesis — title-only exact match still qualifies
        if (string.IsNullOrWhiteSpace(hypothesis.Author))
            return new MatchDetails(Tier, SimilarityScore: null, ContributorMatch: false, ContributorNote: null);

        var primaryAuthor = work.Authors.FirstOrDefault(a => a.IsPrimaryAuthor);
        if (primaryAuthor is null || !_normalizer.AuthorMatches(primaryAuthor.Name, hypothesis.Author))
            return null;

        return new MatchDetails(Tier, SimilarityScore: null, ContributorMatch: false, ContributorNote: null);
    }
}
