namespace BookFinder.Application.Models;

public sealed record SearchBookResponse(IReadOnlyList<RankedCandidate> Results);
