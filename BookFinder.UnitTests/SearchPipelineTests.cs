using BookFinder.Application.Common;
using BookFinder.Application.Explanation;
using BookFinder.Application.Extraction;
using BookFinder.Application.Matching;
using BookFinder.Application.Models;
using BookFinder.Application.Normalization;
using BookFinder.Application.OpenLibrary;
using BookFinder.Application.Pipeline;
using BookFinder.Application.Ranking;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BookFinder.UnitTests;

public class SearchPipelineTests
{
    private static OpenLibraryWork MakeWork(string key, string title, string author)
        => new(key, title,
            [new OpenLibraryAuthor("/authors/OL1A", author, true)],
            null, null, []);

    private static SearchPipeline BuildPipeline(
        ILlmExtractor extractor,
        IOpenLibraryClient olClient,
        ILlmReRanker? reRanker = null)
    {
        var normalizer = new TextNormalizer();
        var rules = new IMatchRule[]
        {
            new Tier1ExactTitlePrimaryAuthorRule(normalizer),
            new Tier2ExactTitleContributorRule(normalizer),
            new Tier3NearMatchTitleAuthorRule(normalizer),
            new Tier4AuthorOnlyRule(normalizer),
            new Tier5NoWinnerRule()
        };
        var hierarchy = new MatchingHierarchy(rules);
        var deduplicator = new Deduplicator();
        var explanationBuilder = new ExplanationBuilder();
        reRanker ??= new NoOpReRanker();

        return new SearchPipeline(
            normalizer, extractor, olClient,
            deduplicator, hierarchy, reRanker, explanationBuilder,
            NullLogger<SearchPipeline>.Instance);
    }

    [Fact]
    public async Task HappyPath_ReturnsRankedResults()
    {
        var work = MakeWork("/works/OL1W", "The Hobbit", "J.R.R. Tolkien");
        var hypothesis = new ExtractedHypothesis("The Hobbit", "Tolkien", [], null);

        var extractor = FakeLlmExtractor.Succeeding(hypothesis);
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Success([work]));

        var pipeline = BuildPipeline(extractor, olClient);

        var result = await pipeline.SearchAsync("tolkien hobbit");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Results);
        Assert.Equal("The Hobbit", result.Value.Results[0].Work.Title);
        Assert.Equal(MatchTier.Tier1ExactTitlePrimaryAuthor, result.Value.Results[0].Match.Tier);
        Assert.NotEmpty(result.Value.Results[0].Explanation);
    }

    [Fact]
    public async Task LlmFailure_FallsBackToNormalizedQuery()
    {
        var work = MakeWork("/works/OL1W", "The Hobbit", "J.R.R. Tolkien");

        var extractor = FakeLlmExtractor.Failing();
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Success([work]));

        var pipeline = BuildPipeline(extractor, olClient);

        var result = await pipeline.SearchAsync("tolkien hobbit");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, extractor.CallCount);

        // Verify the fallback hypothesis reached Open Library — Title and Author must be null,
        // and the normalized tokens from the raw query must be passed as Keywords.
        await olClient.Received(1).SearchAsync(
            Arg.Is<ExtractedHypothesis>(h =>
                h.Title == null &&
                h.Author == null &&
                h.Keywords.Contains("tolkien") &&
                h.Keywords.Contains("hobbit")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenLibraryFailure_ReturnsFailureResult()
    {
        var extractor = FakeLlmExtractor.Succeeding(
            new ExtractedHypothesis("The Hobbit", "Tolkien", [], null));
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Failure(ResultError.OpenLibraryUnavailable));

        var pipeline = BuildPipeline(extractor, olClient);

        var result = await pipeline.SearchAsync("tolkien hobbit");

        Assert.True(result.IsFailure);
        Assert.Equal("openlibrary.unavailable", result.Error!.Code);
    }

    [Fact]
    public async Task EmptyResults_ReturnsSuccessWithEmptyList()
    {
        var extractor = FakeLlmExtractor.Succeeding(
            new ExtractedHypothesis("NonExistentBook", null, [], null));
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Success([]));

        var pipeline = BuildPipeline(extractor, olClient);

        var result = await pipeline.SearchAsync("nonexistentbook");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Results);
    }

    [Fact]
    public async Task ReRankerFailure_KeepsOriginalOrdering()
    {
        var work1 = MakeWork("/works/OL1W", "The Hobbit", "J.R.R. Tolkien");
        var work2 = MakeWork("/works/OL2W", "Lord of the Rings", "J.R.R. Tolkien");
        var hypothesis = new ExtractedHypothesis(null, "Tolkien", [], null);

        var extractor = FakeLlmExtractor.Succeeding(hypothesis);
        var olClient = Substitute.For<IOpenLibraryClient>();
        olClient.SearchAsync(Arg.Any<ExtractedHypothesis>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<OpenLibraryWork>>.Success([work1, work2]));

        var failingReRanker = Substitute.For<ILlmReRanker>();
        failingReRanker.ReRankAsync(Arg.Any<IReadOnlyList<RankedCandidate>>(),
                                    Arg.Any<ExtractedHypothesis>(),
                                    Arg.Any<CancellationToken>())
                       .Returns(Result<IReadOnlyList<string>>.Failure(ResultError.LlmUnavailable));

        var pipeline = BuildPipeline(extractor, olClient, failingReRanker);

        var result = await pipeline.SearchAsync("tolkien");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Results.Count);
        // Original order preserved — both are Tier4 so order determined by OL response
        Assert.Equal("/works/OL1W", result.Value.Results[0].Work.WorkKey);
    }
}
