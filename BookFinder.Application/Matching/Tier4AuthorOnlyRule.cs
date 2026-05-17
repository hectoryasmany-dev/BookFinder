using BookFinder.Application.Models;
using BookFinder.Application.Normalization;

namespace BookFinder.Application.Matching;

public sealed class Tier4AuthorOnlyRule : IMatchRule
{
    private readonly TextNormalizer _normalizer;

    public Tier4AuthorOnlyRule(TextNormalizer normalizer)
        => _normalizer = normalizer;

    public MatchTier Tier => MatchTier.Tier4AuthorOnly;

    public MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis)
    {
        if (hypothesis.Author is null) return null;

        var normHypAuthor = _normalizer.NormalizeAuthor(hypothesis.Author);
        var hasAuthorMatch = work.Authors.Any(a =>
            _normalizer.NormalizeAuthor(a.Name).Contains(normHypAuthor) ||
            normHypAuthor.Contains(_normalizer.NormalizeAuthor(a.Name)));

        if (!hasAuthorMatch) return null;

        return new MatchDetails(Tier, null, false, null);
    }
}
