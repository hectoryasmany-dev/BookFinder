namespace BookFinder.Application.Models;

public sealed record RankedCandidate(
    OpenLibraryWork Work,
    MatchDetails Match,
    string Explanation);
