using BookFinder.Application.Models;
using BookFinder.Application.OpenLibrary;
using ApiContracts = BookFinder.Api.Contracts;

namespace BookFinder.Api.Mapping;

public static class SearchResultMapper
{
    private const string OlBaseUrl = "https://openlibrary.org";

    public static ApiContracts.SearchBookResponse ToDto(SearchBookResponse domain)
        => new(domain.Results.Select(ToDto).ToList());

    public static ApiContracts.SearchResultDto ToDto(RankedCandidate candidate)
    {
        var work = candidate.Work;
        var primaryAuthor = work.Authors.FirstOrDefault(a => a.IsPrimaryAuthor)?.Name
                         ?? work.Authors.FirstOrDefault()?.Name
                         ?? "Unknown";

        var coverUrl = work.CoverId.HasValue
            ? OpenLibraryClient.CoverUrl(work.CoverId.Value)
            : null;

        return new ApiContracts.SearchResultDto(
            Title: work.Title,
            Author: primaryAuthor,
            FirstPublishYear: work.FirstPublishYear,
            Explanation: candidate.Explanation,
            CoverImageUrl: coverUrl,
            OpenLibraryWorkId: work.WorkKey,
            OpenLibraryUrl: $"{OlBaseUrl}{work.WorkKey}");
    }
}
