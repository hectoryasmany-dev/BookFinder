using System.Text.Json;
using BookFinder.Application.Common;
using BookFinder.Application.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BookFinder.Application.Ranking;

public sealed class LlmReRanker : ILlmReRanker
{
    private const string SystemPrompt =
        """
        You are a book relevance re-ranker. Given a user's search intent and a list of book candidates,
        return ONLY a JSON array of work_id strings ordered from most to least relevant.
        Do not invent work IDs. Only use IDs from the provided list.
        """;

    private readonly IChatClient _chatClient;
    private readonly ILogger<LlmReRanker> _logger;

    public LlmReRanker(IChatClient chatClient, ILogger<LlmReRanker> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<string>>> ReRankAsync(
        IReadOnlyList<RankedCandidate> top5,
        ExtractedHypothesis hypothesis,
        CancellationToken ct = default)
    {
        try
        {
            var userPrompt = BuildPrompt(top5, hypothesis);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var options = new ChatOptions
            {
                Temperature = 0.0f,
                ResponseFormat = ChatResponseFormat.Json
            };

            var response = await _chatClient.GetResponseAsync(messages, options, ct);
            var text = response.Text;

            if (string.IsNullOrWhiteSpace(text))
                return Result<IReadOnlyList<string>>.Failure(ResultError.LlmInvalidResponse);

            var ids = JsonSerializer.Deserialize<List<string>>(text);
            if (ids is null || ids.Count == 0)
                return Result<IReadOnlyList<string>>.Failure(ResultError.LlmInvalidResponse);

            var validKeys = top5.Select(c => c.Work.WorkKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var filtered = ids.Where(id => validKeys.Contains(id)).ToList();

            return Result<IReadOnlyList<string>>.Success(filtered);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM re-ranking failed; keeping original order");
            return Result<IReadOnlyList<string>>.Failure(ResultError.LlmUnavailable);
        }
    }

    private static string BuildPrompt(IReadOnlyList<RankedCandidate> candidates, ExtractedHypothesis hypothesis)
    {
        var intent = $"Title: {hypothesis.Title ?? "unknown"}, Author: {hypothesis.Author ?? "unknown"}, Keywords: {string.Join(", ", hypothesis.Keywords)}";
        var list = string.Join("\n", candidates.Select(c =>
            $"- work_id: {c.Work.WorkKey}, title: \"{c.Work.Title}\", author: \"{c.Work.Authors.FirstOrDefault()?.Name ?? "unknown"}\""));

        return $"User intent: {intent}\n\nCandidates:\n{list}\n\nReturn a JSON array of work_id strings, best match first.";
    }
}
