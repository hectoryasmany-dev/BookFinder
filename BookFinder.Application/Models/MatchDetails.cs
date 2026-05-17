namespace BookFinder.Application.Models;

public sealed record MatchDetails(
    MatchTier Tier,
    double? SimilarityScore,
    bool ContributorMatch,
    string? ContributorNote);
