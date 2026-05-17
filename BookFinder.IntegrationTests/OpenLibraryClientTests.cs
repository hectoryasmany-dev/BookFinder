using System.Net;
using System.Text;
using BookFinder.Application.Configuration;
using BookFinder.Application.Models;
using BookFinder.Application.OpenLibrary;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookFinder.IntegrationTests;

public class OpenLibraryClientTests
{
    private static OpenLibraryClient BuildClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openlibrary.org")
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<OpenLibraryClient>.Instance;
        var options = Options.Create(new OpenLibraryOptions { CacheMinutes = 60 });
        return new OpenLibraryClient(httpClient, cache, logger, options);
    }

    [Fact]
    public async Task SearchAsync_HobbitHypothesis_ReturnsTolkienWork()
    {
        var handler = new RoutingFakeHandler(new Dictionary<string, string>
        {
            ["/search.json"] = ReadTestData("openlibrary-hobbit-search.json"),
            ["/works/OL262758W.json"] = ReadTestData("openlibrary-hobbit-work.json"),
            ["/authors/OL26320A.json"] = ReadTestData("openlibrary-tolkien-author.json")
        });

        var client = BuildClient(handler);
        var hypothesis = new ExtractedHypothesis("The Hobbit", "Tolkien", [], 1937);

        var result = await client.SearchAsync(hypothesis);

        result.IsSuccess.Should().BeTrue();
        var works = result.Value!;
        works.Should().HaveCount(1);
        works[0].Title.Should().Be("The Hobbit");
        works[0].Authors.Should().Contain(a => a.Name == "J.R.R. Tolkien" && a.IsPrimaryAuthor);
        works[0].FirstPublishYear.Should().Be(1937);
    }

    [Fact]
    public async Task SearchAsync_NetworkFailure_ReturnsOpenLibraryUnavailable()
    {
        var handler = new AlwaysFailHandler();
        var client = BuildClient(handler);
        var hypothesis = new ExtractedHypothesis("The Hobbit", null, [], null);

        var result = await client.SearchAsync(hypothesis);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("openlibrary.unavailable");
    }

    [Fact]
    public async Task SearchAsync_CachesSecondCall()
    {
        var handler = new RoutingFakeHandler(new Dictionary<string, string>
        {
            ["/search.json"] = ReadTestData("openlibrary-hobbit-search.json"),
            ["/works/OL262758W.json"] = ReadTestData("openlibrary-hobbit-work.json"),
            ["/authors/OL26320A.json"] = ReadTestData("openlibrary-tolkien-author.json")
        });

        var client = BuildClient(handler);
        var hypothesis = new ExtractedHypothesis("The Hobbit", "Tolkien", [], null);

        await client.SearchAsync(hypothesis);
        await client.SearchAsync(hypothesis);

        // Only one search request (the rest are enrichment calls, always made fresh)
        handler.CallCount("/search.json").Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var handler = new SlowHandler();
        var client = BuildClient(handler);
        var hypothesis = new ExtractedHypothesis("anything", null, [], null);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SearchAsync(hypothesis, cts.Token));
    }

    private static string ReadTestData(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", filename);
        return File.ReadAllText(path);
    }
}

internal sealed class RoutingFakeHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _routes;
    private readonly Dictionary<string, int> _callCounts = new();

    public RoutingFakeHandler(Dictionary<string, string> routes) => _routes = routes;

    public int CallCount(string pathPrefix)
        => _callCounts.Where(kv => kv.Key.StartsWith(pathPrefix)).Sum(kv => kv.Value);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = request.RequestUri!.AbsolutePath;
        _callCounts.TryGetValue(path, out var count);
        _callCounts[path] = count + 1;

        foreach (var (prefix, body) in _routes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal sealed class AlwaysFailHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => throw new HttpRequestException("Simulated network failure");
}

internal sealed class SlowHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
