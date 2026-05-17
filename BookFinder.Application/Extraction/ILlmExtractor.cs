using BookFinder.Application.Common;
using BookFinder.Application.Models;

namespace BookFinder.Application.Extraction;

public interface ILlmExtractor
{
    Task<Result<ExtractedHypothesis>> ExtractAsync(string blob, CancellationToken ct = default);
}
