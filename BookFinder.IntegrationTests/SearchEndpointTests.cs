using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookFinder.Application.Common;
using BookFinder.Application.Extraction;
using BookFinder.Application.Models;
using BookFinder.Application.OpenLibrary;
using BookFinder.Application.Pipeline;
using BookFinder.Application.Ranking;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ApiContracts = BookFinder.Api.Contracts;

namespace BookFinder.IntegrationTests;

public class SearchEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SearchEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient BuildClient(
        ILlmExtractor? extractor = null,
        IOpenLibraryClient? olClient = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(Substitute.For<IChatClient>());
                services.AddSingleton<ILlmReRanker, NoOpReRanker>();
                if (extractor is not null)
                    services.AddSingleton<ILlmExtractor>(extractor);
                if (olClient is not null)
                    services.AddSingleton<IOpenLibraryClient>(olClient);
            });
        }).CreateClient();
    }

    private static OpenLibraryWork MakeWork(string title, string author)
        => new("/works/OL1W", title,
            [new OpenLibraryAuthor("/authors/OL1A", author, true)],
            1937, null, []);

    [Fact]
    public async Task Search_ValidQuery_Returns200WithResults()
    {
        var work = MakeWork("The Hobbit", "J.R.R. Tolkien");
        var hypothesis = new ExtractedHypothesis("The Hobbit", "Tolkien", [], null);

        var extractor = FakeLlmExtractor.Succeeding(hypothesis);
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Success([work]));

        var client = BuildClient(extractor, olClient);

        var response = await client.PostAsJsonAsync("/api/v1/books/search",
            new { query = "tolkien hobbit" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiContracts.SearchBookResponse>();
        body!.Results.Should().HaveCount(1);
        body.Results[0].Title.Should().Be("The Hobbit");
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns400()
    {
        var client = BuildClient();

        var response = await client.PostAsJsonAsync("/api/v1/books/search",
            new { query = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_NullQuery_Returns400()
    {
        var client = BuildClient();

        var response = await client.PostAsJsonAsync("/api/v1/books/search",
            new { query = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_OpenLibraryUnavailable_Returns502()
    {
        var extractor = FakeLlmExtractor.Succeeding(
            new ExtractedHypothesis("The Hobbit", null, [], null));
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Failure(ResultError.OpenLibraryUnavailable));

        var client = BuildClient(extractor, olClient);

        var response = await client.PostAsJsonAsync("/api/v1/books/search",
            new { query = "tolkien hobbit" });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Search_Cancellation_PropagatesDownstream()
    {
        var olClient = Substitute.For<IOpenLibraryClient>();
        var cts = new CancellationTokenSource();

        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(async ci =>
                {
                    var ct = ci.Arg<CancellationToken>();
                    await Task.Delay(Timeout.Infinite, ct);
                    return Result<IReadOnlyList<OpenLibraryWork>>.Success([]);
                });

        var extractor = FakeLlmExtractor.Succeeding(
            new ExtractedHypothesis("The Hobbit", null, [], null));

        var client = BuildClient(extractor, olClient);
        cts.CancelAfter(100);

        var act = () => client.PostAsJsonAsync("/api/v1/books/search",
            new { query = "tolkien hobbit" }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
