namespace BookFinder.Application.Models;

public sealed record ExtractedHypothesis(
    string? Title,
    string? Author,
    string[] Keywords,
    int? Year);
