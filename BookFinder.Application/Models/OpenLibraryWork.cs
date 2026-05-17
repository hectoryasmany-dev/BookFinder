namespace BookFinder.Application.Models;

public sealed record OpenLibraryWork(
    string WorkKey,
    string Title,
    IReadOnlyList<OpenLibraryAuthor> Authors,
    int? FirstPublishYear,
    int? CoverId,
    string[] Subjects);
