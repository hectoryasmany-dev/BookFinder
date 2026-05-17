namespace BookFinder.Application.Models;

public sealed record NormalizedQuery(string Raw, string Normalized, string[] Tokens);
