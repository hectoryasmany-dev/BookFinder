using BookFinder.Application.Common;
using BookFinder.Application.Models;

namespace BookFinder.Application.Ranking;

public interface ILlmReRanker
{
    Task<Result<IReadOnlyList<string>>> ReRankAsync(
        IReadOnlyList<RankedCandidate> top5,
        ExtractedHypothesis hypothesis,
        CancellationToken ct = default);
}
