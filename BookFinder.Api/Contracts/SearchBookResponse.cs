namespace BookFinder.Api.Contracts;

public sealed record SearchBookResponse(IReadOnlyList<SearchResultDto> Results);

public sealed record SearchResultDto(
    string Title,
    string Author,
    int? FirstPublishYear,
    string Explanation,
    string? CoverImageUrl,
    string OpenLibraryWorkId,
    string OpenLibraryUrl);
