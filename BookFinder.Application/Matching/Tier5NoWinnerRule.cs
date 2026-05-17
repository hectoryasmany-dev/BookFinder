using BookFinder.Application.Models;

namespace BookFinder.Application.Matching;

public sealed class Tier5NoWinnerRule : IMatchRule
{
    public MatchTier Tier => MatchTier.Tier5NoWinner;

    public MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis)
        => new MatchDetails(Tier, null, false, null);
}
