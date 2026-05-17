using System.Text.Json;
using BookFinder.Application.Common;
using BookFinder.Application.Configuration;
using BookFinder.Application.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookFinder.Application.Extraction;

public sealed class LlmExtractor : ILlmExtractor
{
    private const string SystemPrompt =
        """
        You are a book-search assistant. Extract structured book search fields from the user's raw text.
        Correct obvious typos in author names and titles (e.g. "toolkien" → "Tolkien").
        Return ONLY valid JSON with keys: title (string|null), author (string|null), keywords (string[]), year (int|null).
        No markdown, no explanation.

        Examples:
        Input: "tolkien hobbit illustrated deluxe 1937"
        Output: {"title":"The Hobbit","author":"J.R.R. Tolkien","keywords":["illustrated","deluxe"],"year":1937}

        Input: "mark huckleberry"
        Output: {"title":null,"author":"Mark Twain","keywords":["huckleberry"],"year":null}

        Input: "dickens, tale two cities"
        Output: {"title":"A Tale of Two Cities","author":"Charles Dickens","keywords":[],"year":null}

        Input: "toolkien lord rings"
        Output: {"title":"The Lord of the Rings","author":"J.R.R. Tolkien","keywords":[],"year":null}
        """;

    private readonly IChatClient _chatClient;
    private readonly ILogger<LlmExtractor> _logger;
    private readonly int _timeoutSeconds;

    public LlmExtractor(IChatClient chatClient, IOptions<AiProviderOptions> options, ILogger<LlmExtractor> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _timeoutSeconds = options.Value.LlmTimeoutSeconds;
    }

    public async Task<Result<ExtractedHypothesis>> ExtractAsync(string blob, CancellationToken ct = default)
    {
        // Hard cap: if the LLM doesn't respond within the configured timeout, fall back
        // to normalized-query matching. Configurable via AI:LlmTimeoutSeconds in appsettings.
        // Free-tier Gemini may queue for up to 45s;Protects also from the controller timing out and getting 500 error
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, blob)
            };

            var options = new ChatOptions
            {
                Temperature = 0.1f,
                ResponseFormat = ChatResponseFormat.Json
            };

            var response = await _chatClient.GetResponseAsync(messages, options, timeout.Token);
            var text = response.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("LLM returned empty response for blob length {Length}", blob.Length);
                return ResultError.LlmInvalidResponse;
            }

            var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var tp) && tp.ValueKind == JsonValueKind.String
                ? tp.GetString() : null;
            var author = root.TryGetProperty("author", out var ap) && ap.ValueKind == JsonValueKind.String
                ? ap.GetString() : null;
            var year = root.TryGetProperty("year", out var yp) && yp.ValueKind == JsonValueKind.Number
                ? yp.GetInt32() : (int?)null;
            var keywords = root.TryGetProperty("keywords", out var kp) && kp.ValueKind == JsonValueKind.Array
                ? kp.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => s.Length > 0)
                    .ToArray()
                : [];

            return new ExtractedHypothesis(title, author, keywords, year);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Request was cancelled by the caller (e.g. user closed browser) — propagate
            throw;
        }
        catch (OperationCanceledException)
        {
            
            _logger.LogWarning("LLM extraction timed out after {Timeout}s; falling back to normalized query", _timeoutSeconds);
            return ResultError.LlmUnavailable;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM returned unparsable JSON");
            return ResultError.LlmInvalidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM extraction failed");
            return ResultError.LlmUnavailable;
        }
    }
}
