using System.Diagnostics;
using BookFinder.Application.Common;
using BookFinder.Application.Explanation;
using BookFinder.Application.Extraction;
using BookFinder.Application.Matching;
using BookFinder.Application.Models;
using BookFinder.Application.Normalization;
using BookFinder.Application.OpenLibrary;
using BookFinder.Application.Ranking;
using Microsoft.Extensions.Logging;

namespace BookFinder.Application.Pipeline;

public sealed class SearchPipeline : ISearchPipeline
{
    private const int MaxResults = 5;

    private readonly TextNormalizer _normalizer;
    private readonly ILlmExtractor _extractor;
    private readonly IOpenLibraryClient _olClient;
    private readonly Deduplicator _deduplicator;
    private readonly MatchingHierarchy _matchingHierarchy;
    private readonly ILlmReRanker _reRanker;
    private readonly ExplanationBuilder _explanationBuilder;
    private readonly ILogger<SearchPipeline> _logger;

    public SearchPipeline(
        TextNormalizer normalizer,
        ILlmExtractor extractor,
        IOpenLibraryClient olClient,
        Deduplicator deduplicator,
        MatchingHierarchy matchingHierarchy,
        ILlmReRanker reRanker,
        ExplanationBuilder explanationBuilder,
        ILogger<SearchPipeline> logger)
    {
        _normalizer = normalizer;
        _extractor = extractor;
        _olClient = olClient;
        _deduplicator = deduplicator;
        _matchingHierarchy = matchingHierarchy;
        _reRanker = reRanker;
        _explanationBuilder = explanationBuilder;
        _logger = logger;
    }

    public async Task<Result<SearchBookResponse>> SearchAsync(string query, CancellationToken ct = default)
    {
        // Stage 1 — normalize
        var normalized = Time("normalize", () => _normalizer.Normalize(query));

        // Stage 2 — LLM extraction (fall back to normalized query on failure)
        var hypothesis = await ExtractHypothesisAsync(query, normalized, ct);

        // Stage 3 — Open Library search
        var searchResult = await TimeAsync("ol_search",
            () => _olClient.SearchAsync(hypothesis, ct));

        if (searchResult.IsFailure)
            return Result<SearchBookResponse>.Failure(searchResult.Error!);

        var works = searchResult.Value!;

        if (works.Count == 0)
            return Result<SearchBookResponse>.Success(new SearchBookResponse([]));

        // Stage 4 — deduplicate
        var deduped = Time("deduplicate", () => _deduplicator.Deduplicate(works));

        // Stage 5 — matching hierarchy
        var ranked = Time("match", () => _matchingHierarchy.Rank(deduped, hypothesis));

        var top5 = ranked.Take(MaxResults).ToList();

        // Stage 6 — optional LLM re-ranking
        top5 = await ReRankAsync(top5, hypothesis, ct);

        // Stage 7 — build explanations
        var results = top5
            .Select(c => c with { Explanation = _explanationBuilder.Build(c, hypothesis) })
            .ToList();

        return Result<SearchBookResponse>.Success(new SearchBookResponse(results));
    }

    private async Task<ExtractedHypothesis> ExtractHypothesisAsync(
        string blob, Models.NormalizedQuery normalized, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var extractResult = await _extractor.ExtractAsync(blob, ct);
        sw.Stop();

        if (extractResult.IsSuccess)
        {
            var h = extractResult.Value!;
            _logger.LogInformation(
                "Stage llm_extract completed in {Ms}ms → title={Title} author={Author} year={Year} keywords=[{Keywords}]",
                sw.ElapsedMilliseconds, h.Title ?? "null", h.Author ?? "null", h.Year?.ToString() ?? "null",
                string.Join(", ", h.Keywords));
            return h;
        }

        _logger.LogWarning("LLM extraction failed ({Code}); falling back to normalized query in {Ms}ms",
            extractResult.Error!.Code, sw.ElapsedMilliseconds);

        return new ExtractedHypothesis(
            Title: null,
            Author: null,
            Keywords: normalized.Tokens,
            Year: null);
    }

    private async Task<List<RankedCandidate>> ReRankAsync(
        List<RankedCandidate> top5, ExtractedHypothesis hypothesis, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var reRankResult = await _reRanker.ReRankAsync(top5, hypothesis, ct);
        sw.Stop();

        if (reRankResult.IsFailure)
        {
            _logger.LogWarning("Re-ranker failed ({Code}); keeping original order in {Ms}ms",
                reRankResult.Error!.Code, sw.ElapsedMilliseconds);
            return top5;
        }

        _logger.LogInformation("Stage llm_rerank completed in {Ms}ms", sw.ElapsedMilliseconds);

        var orderedIds = reRankResult.Value!;
        var byKey = top5.ToDictionary(c => c.Work.WorkKey, StringComparer.OrdinalIgnoreCase);

        var reordered = orderedIds
            .Where(id => byKey.ContainsKey(id))
            .Select(id => byKey[id])
            .ToList();

        // Append any candidates the re-ranker omitted
        var included = orderedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        reordered.AddRange(top5.Where(c => !included.Contains(c.Work.WorkKey)));

        return reordered;
    }

    private T Time<T>(string stage, Func<T> fn)
    {
        var sw = Stopwatch.StartNew();
        var result = fn();
        _logger.LogInformation("Stage {Stage} completed in {Ms}ms", stage, sw.ElapsedMilliseconds);
        return result;
    }

    private async Task<T> TimeAsync<T>(string stage, Func<Task<T>> fn)
    {
        var sw = Stopwatch.StartNew();
        var result = await fn();
        _logger.LogInformation("Stage {Stage} completed in {Ms}ms", stage, sw.ElapsedMilliseconds);
        return result;
    }
}
