using BookFinder.Application.Common;
using BookFinder.Application.Models;

namespace BookFinder.Application.Ranking;

public sealed class NoOpReRanker : ILlmReRanker
{
    public Task<Result<IReadOnlyList<string>>> ReRankAsync(
        IReadOnlyList<RankedCandidate> top5,
        ExtractedHypothesis hypothesis,
        CancellationToken ct = default)
    {
        var ids = top5.Select(c => c.Work.WorkKey).ToList();
        return Task.FromResult(Result<IReadOnlyList<string>>.Success((IReadOnlyList<string>)ids));
    }
}
