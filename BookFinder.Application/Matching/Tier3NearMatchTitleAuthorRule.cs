using BookFinder.Application.Models;
using BookFinder.Application.Normalization;
using F23.StringSimilarity;

namespace BookFinder.Application.Matching;

public sealed class Tier3NearMatchTitleAuthorRule : IMatchRule
{
    private const double JaroWinklerThreshold = 0.85;
    private static readonly JaroWinkler JaroWinkler = new();

    private readonly TextNormalizer _normalizer;

    public Tier3NearMatchTitleAuthorRule(TextNormalizer normalizer)
        => _normalizer = normalizer;

    public MatchTier Tier => MatchTier.Tier3NearMatchTitleAuthor;

    public MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis)
    {
        if (hypothesis.Title is null) return null;

        var normHypTitle = _normalizer.NormalizeTitle(hypothesis.Title);
        var normWorkTitle = _normalizer.NormalizeTitle(work.Title);

        var similarity = JaroWinkler.Similarity(normHypTitle, normWorkTitle);
        if (similarity < JaroWinklerThreshold) return null;

        if (hypothesis.Author is null)
            return new MatchDetails(Tier, similarity, false, null);

        var normHypAuthor = _normalizer.NormalizeAuthor(hypothesis.Author);
        var hasAuthorMatch = work.Authors.Any(a =>
            _normalizer.NormalizeAuthor(a.Name).Contains(normHypAuthor) ||
            normHypAuthor.Contains(_normalizer.NormalizeAuthor(a.Name)));

        if (!hasAuthorMatch) return null;

        return new MatchDetails(Tier, similarity, false, null);
    }
}
