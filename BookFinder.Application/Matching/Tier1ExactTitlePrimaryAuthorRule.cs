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
        if (hypothesis.Title is null) return null;

        var normHypTitle = _normalizer.NormalizeTitle(hypothesis.Title);
        var normWorkTitle = _normalizer.NormalizeTitle(work.Title);

        if (normHypTitle != normWorkTitle) return null;

        if (hypothesis.Author is null)
            return new MatchDetails(Tier, null, false, null);

        var normHypAuthor = _normalizer.NormalizeAuthor(hypothesis.Author);
        var primaryAuthor = work.Authors.FirstOrDefault(a => a.IsPrimaryAuthor);
        if (primaryAuthor is null) return null;

        var normWorkAuthor = _normalizer.NormalizeAuthor(primaryAuthor.Name);
        if (!normWorkAuthor.Contains(normHypAuthor) && !normHypAuthor.Contains(normWorkAuthor))
            return null;

        return new MatchDetails(Tier, null, false, null);
    }
}
