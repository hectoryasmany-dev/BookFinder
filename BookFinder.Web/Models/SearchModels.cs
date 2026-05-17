namespace BookFinder.Web.Models;

public sealed record SearchBookRequest(string Query);

public sealed record SearchBookResponse(IReadOnlyList<SearchResultDto> Results);

public sealed record SearchResultDto(
    string Title,
    string Author,
    int? FirstPublishYear,
    string Explanation,
    string? CoverImageUrl,
    string OpenLibraryWorkId,
    string OpenLibraryUrl);
