using BookFinder.Application.Models;

namespace BookFinder.Application.Matching;

public interface IMatchRule
{
    MatchTier Tier { get; }
    MatchDetails? TryMatch(OpenLibraryWork work, ExtractedHypothesis hypothesis);
}
