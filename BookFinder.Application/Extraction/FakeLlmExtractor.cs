using BookFinder.Application.Common;
using BookFinder.Application.Models;

namespace BookFinder.Application.Extraction;

public sealed class FakeLlmExtractor : ILlmExtractor
{
    private readonly Result<ExtractedHypothesis> _result;
    public int CallCount { get; private set; }

    private FakeLlmExtractor(Result<ExtractedHypothesis> result) => _result = result;

    public static FakeLlmExtractor Succeeding(ExtractedHypothesis hypothesis)
        => new(Result<ExtractedHypothesis>.Success(hypothesis));

    public static FakeLlmExtractor Failing()
        => new(Result<ExtractedHypothesis>.Failure(ResultError.LlmUnavailable));

    public Task<Result<ExtractedHypothesis>> ExtractAsync(string blob, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        CallCount++;
        return Task.FromResult(_result);
    }
}
