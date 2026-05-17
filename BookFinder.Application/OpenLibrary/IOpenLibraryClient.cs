using BookFinder.Application.Common;
using BookFinder.Application.Models;

namespace BookFinder.Application.OpenLibrary;

public interface IOpenLibraryClient
{
    Task<Result<IReadOnlyList<OpenLibraryWork>>> SearchAsync(
        ExtractedHypothesis hypothesis,
        CancellationToken ct = default);
}
