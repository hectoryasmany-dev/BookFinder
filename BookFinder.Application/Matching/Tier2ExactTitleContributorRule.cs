using BookFinder.Application.Models;
using BookFinder.Application.Normalization;

namespace BookFinder.Application.Matching;

public sealed class Tier2ExactTitleContributorRule : IMatchRule
{
    private readonly TextNormalizer _normalizer;

    public Tier2ExactTitleContributorRule(TextNormalizer normalizer)
        => _normalizer = normalizer;

    public MatchTier Tier => MatchTier.Tier2ExactTitleContributor;

    public MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis)
    {
        if (hypothesis.Title is null || hypothesis.Author is null) return null;

        var normHypTitle = _normalizer.NormalizeTitle(hypothesis.Title);
        var normWorkTitle = _normalizer.NormalizeTitle(work.Title);
        if (normHypTitle != normWorkTitle) return null;

        var normHypAuthor = _normalizer.NormalizeAuthor(hypothesis.Author);
        var contributor = work.Authors.FirstOrDefault(a =>
            !a.IsPrimaryAuthor &&
            (_normalizer.NormalizeAuthor(a.Name).Contains(normHypAuthor) ||
             normHypAuthor.Contains(_normalizer.NormalizeAuthor(a.Name))));

        if (contributor is null) return null;

        return new MatchDetails(Tier, null, true, $"{contributor.Name} listed as contributor.");
    }
}
