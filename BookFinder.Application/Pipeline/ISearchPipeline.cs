using BookFinder.Application.Common;
using BookFinder.Application.Models;

namespace BookFinder.Application.Pipeline;

public interface ISearchPipeline
{
    Task<Result<SearchBookResponse>> SearchAsync(string query, CancellationToken ct = default);
}
