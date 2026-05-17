using System.Net.Http.Json;
using System.Text;
using BookFinder.Application.Common;
using BookFinder.Application.Configuration;
using BookFinder.Application.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookFinder.Application.OpenLibrary;

public sealed class OpenLibraryClient : IOpenLibraryClient
{
    private const int MaxSearchResults = 20;
    private const string CoverBaseUrl = "https://covers.openlibrary.org/b/id";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenLibraryClient> _logger;
    private readonly OpenLibraryOptions _options;

    public OpenLibraryClient(
        HttpClient http,
        IMemoryCache cache,
        ILogger<OpenLibraryClient> logger,
        IOptions<OpenLibraryOptions> options)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<OpenLibraryWork>>> SearchAsync(
        ExtractedHypothesis hypothesis,
        CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(hypothesis);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<OpenLibraryWork>? cached) && cached is not null)
            return Result<IReadOnlyList<OpenLibraryWork>>.Success(cached);

        try
        {
            var url = BuildSearchUrl(hypothesis);
            var response = await _http.GetFromJsonAsync<OlSearchResponse>(url, ct);

            if (response is null || response.Docs.Count == 0)
                return Result<IReadOnlyList<OpenLibraryWork>>.Success([]);

            var docs = response.Docs.Take(MaxSearchResults).ToList();
            var works = await EnrichWorksAsync(docs, ct);

            _cache.Set(cacheKey, works, TimeSpan.FromMinutes(_options.CacheMinutes));

            return Result<IReadOnlyList<OpenLibraryWork>>.Success(works);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open Library search failed for hypothesis {@Hypothesis}", new { hypothesis.Title, hypothesis.Author });
            return Result<IReadOnlyList<OpenLibraryWork>>.Failure(ResultError.OpenLibraryUnavailable);
        }
    }

    private async Task<IReadOnlyList<OpenLibraryWork>> EnrichWorksAsync(
        List<OlSearchDoc> docs,
        CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(5, 5);

        var tasks = docs.Select(async doc =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var authors = await ResolveAuthorsAsync(doc, ct);
                return new OpenLibraryWork(
                    WorkKey: doc.Key,
                    Title: doc.Title,
                    Authors: authors,
                    FirstPublishYear: doc.FirstPublishYear,
                    CoverId: doc.CoverId,
                    Subjects: doc.Subject?.ToArray() ?? []);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var works = await Task.WhenAll(tasks);

        return works;
    }

    private async Task<IReadOnlyList<OpenLibraryAuthor>> ResolveAuthorsAsync(
        OlSearchDoc doc,
        CancellationToken ct)
    {
        // Try to get canonical author data from the works endpoint
        var workId = ExtractId(doc.Key);
        if (!string.IsNullOrEmpty(workId))
        {
            try
            {
                var workDetail = await _http.GetFromJsonAsync<OlWorkDetail>($"/works/{workId}.json", ct);
                if (workDetail?.Authors is { Count: > 0 })
                    return await ResolveFromWorkDetailAsync(workDetail, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* fall through to search doc authors */ }
        }

        // Fall back to search doc author_name / author_key lists
        return BuildAuthorsFromSearchDoc(doc);
    }

    private async Task<IReadOnlyList<OpenLibraryAuthor>> ResolveFromWorkDetailAsync(
        OlWorkDetail workDetail,
        CancellationToken ct)
    {
        var result = new List<OpenLibraryAuthor>();
        var isPrimary = true;

        foreach (var entry in workDetail.Authors ?? [])
        {
            if (entry.Author?.Key is null) continue;

            var authorId = ExtractId(entry.Author.Key);
            if (string.IsNullOrEmpty(authorId)) continue;

            try
            {
                var authorDetail = await _http.GetFromJsonAsync<OlAuthorDetail>($"/authors/{authorId}.json", ct);
                if (authorDetail is not null)
                {
                    result.Add(new OpenLibraryAuthor(
                        AuthorKey: entry.Author.Key,
                        Name: authorDetail.Name,
                        IsPrimaryAuthor: isPrimary && string.IsNullOrEmpty(entry.Role)));
                    isPrimary = false;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* skip this author */ }
        }

        return result;
    }

    private static IReadOnlyList<OpenLibraryAuthor> BuildAuthorsFromSearchDoc(OlSearchDoc doc)
    {
        if (doc.AuthorName is null || doc.AuthorName.Count == 0)
            return [];

        return doc.AuthorName
            .Select((name, i) => new OpenLibraryAuthor(
                AuthorKey: doc.AuthorKey?.ElementAtOrDefault(i) ?? string.Empty,
                Name: name,
                IsPrimaryAuthor: i == 0))
            .ToList();
    }

    private static string BuildSearchUrl(ExtractedHypothesis hypothesis)
    {
        var sb = new StringBuilder("/search.json?limit=20");

        if (!string.IsNullOrWhiteSpace(hypothesis.Title))
            sb.Append("&title=").Append(Uri.EscapeDataString(hypothesis.Title));

        if (!string.IsNullOrWhiteSpace(hypothesis.Author))
            sb.Append("&author=").Append(Uri.EscapeDataString(hypothesis.Author));

        if (hypothesis.Keywords.Length > 0)
            sb.Append("&q=").Append(Uri.EscapeDataString(string.Join(" ", hypothesis.Keywords)));

        return sb.ToString();
    }

    private static string BuildCacheKey(ExtractedHypothesis hypothesis)
        => $"ol:{hypothesis.Title}:{hypothesis.Author}:{string.Join(",", hypothesis.Keywords)}:{hypothesis.Year}";

    private static string ExtractId(string key)
    {
        var parts = key.TrimStart('/').Split('/');
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    public static string CoverUrl(int coverId) => $"{CoverBaseUrl}/{coverId}-M.jpg";
}
