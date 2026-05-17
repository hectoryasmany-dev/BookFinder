using BookFinder.Application.Explanation;
using BookFinder.Application.Models;

namespace BookFinder.UnitTests;

public class ExplanationBuilderTests
{
    private readonly ExplanationBuilder _sut = new();

    private static OpenLibraryWork Work(string title, params (string name, bool primary)[] authors)
        => new("/works/OL1W", title,
            authors.Select(a => new OpenLibraryAuthor("/authors/OL1A", a.name, a.primary)).ToList(),
            null, null, []);

    private static ExtractedHypothesis Hypo(string? title = null, string? author = null)
        => new(title, author, [], null);

    [Fact]
    public void Tier1_IncludesExactTitleAndPrimaryAuthor()
    {
        var candidate = new RankedCandidate(
            Work("The Hobbit", ("J.R.R. Tolkien", true)),
            new MatchDetails(MatchTier.Tier1ExactTitlePrimaryAuthor, null, false, null),
            string.Empty);

        var explanation = _sut.Build(candidate, Hypo("the hobbit", "tolkien"));

        Assert.Contains("Exact title match", explanation);
        Assert.Contains("J.R.R. Tolkien", explanation);
    }

    [Fact]
    public void Tier2_IncludesContributorNote()
    {
        var candidate = new RankedCandidate(
            Work("The Hobbit", ("J.R.R. Tolkien", true), ("Alan Lee", false)),
            new MatchDetails(MatchTier.Tier2ExactTitleContributor, null, true, "Alan Lee listed as contributor."),
            string.Empty);

        var explanation = _sut.Build(candidate, Hypo("the hobbit", "alan lee"));

        Assert.Contains("Exact title match", explanation);
        Assert.Contains("Alan Lee", explanation);
    }

    [Fact]
    public void Tier3_IncludesSimilarityScore()
    {
        var candidate = new RankedCandidate(
            Work("Great Expectations", ("Charles Dickens", true)),
            new MatchDetails(MatchTier.Tier3NearMatchTitleAuthor, 0.91, false, null),
            string.Empty);

        var explanation = _sut.Build(candidate, Hypo("great expectation", "dickens"));

        Assert.Contains("Near-match title", explanation);
        Assert.Contains("0.91", explanation);
        Assert.Contains("Charles Dickens", explanation);
    }

    [Fact]
    public void Tier4_IncludesAuthorName()
    {
        var candidate = new RankedCandidate(
            Work("Oliver Twist", ("Charles Dickens", true)),
            new MatchDetails(MatchTier.Tier4AuthorOnly, null, false, null),
            string.Empty);

        var explanation = _sut.Build(candidate, Hypo(author: "dickens"));

        Assert.Contains("Author-only match", explanation);
        Assert.Contains("Charles Dickens", explanation);
    }

    [Fact]
    public void Tier5_IncludesKeywordsOrFallback()
    {
        var candidate = new RankedCandidate(
            Work("Random Book", ("Someone", true)),
            new MatchDetails(MatchTier.Tier5NoWinner, null, false, null),
            string.Empty);
        var hypo = new ExtractedHypothesis(null, null, ["fantasy", "dragon"], null);

        var explanation = _sut.Build(candidate, hypo);

        Assert.Contains("No strong match", explanation);
        Assert.Contains("fantasy", explanation);
    }
}
