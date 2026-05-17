using BookFinder.Application.Extraction;
using BookFinder.Application.Models;

namespace BookFinder.UnitTests;

public class FakeLlmExtractorTests
{
    [Fact]
    public async Task Failing_ReturnsLlmUnavailableError()
    {
        var extractor = FakeLlmExtractor.Failing();

        var result = await extractor.ExtractAsync("tolkien hobbit");

        Assert.True(result.IsFailure);
        Assert.Equal("llm.unavailable", result.Error!.Code);
    }

    [Fact]
    public async Task Succeeding_ReturnsExpectedHypothesis()
    {
        var hypothesis = new ExtractedHypothesis("The Hobbit", "J.R.R. Tolkien", ["illustrated"], 1937);
        var extractor = FakeLlmExtractor.Succeeding(hypothesis);

        var result = await extractor.ExtractAsync("tolkien hobbit illustrated 1937");

        Assert.True(result.IsSuccess);
        Assert.Equal("The Hobbit", result.Value!.Title);
        Assert.Equal("J.R.R. Tolkien", result.Value.Author);
        Assert.Equal(1937, result.Value.Year);
    }

    [Fact]
    public async Task CallCount_IncrementsPerCall()
    {
        var extractor = FakeLlmExtractor.Succeeding(
            new ExtractedHypothesis(null, null, [], null));

        await extractor.ExtractAsync("first");
        await extractor.ExtractAsync("second");
        await extractor.ExtractAsync("third");

        Assert.Equal(3, extractor.CallCount);
    }

    [Fact]
    public async Task Failing_ThrowsOnCancellation()
    {
        var extractor = FakeLlmExtractor.Failing();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractor.ExtractAsync("any", cts.Token));
    }
}
