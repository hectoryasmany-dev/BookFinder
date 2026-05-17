using BookFinder.Application.Matching;
using BookFinder.Application.Models;
using BookFinder.Application.Normalization;

namespace BookFinder.UnitTests;

public class MatchingHierarchyTests
{
    private readonly TextNormalizer _normalizer = new();

    private MatchingHierarchy BuildHierarchy()
    {
        var rules = new IMatchRule[]
        {
            new Tier1ExactTitlePrimaryAuthorRule(_normalizer),
            new Tier2ExactTitleContributorRule(_normalizer),
            new Tier3NearMatchTitleAuthorRule(_normalizer),
            new Tier4AuthorOnlyRule(_normalizer),
            new Tier5NoWinnerRule()
        };
        return new MatchingHierarchy(rules);
    }

    private static OpenLibraryWork Work(string title, params (string name, bool primary)[] authors)
        => new(
            WorkKey: $"/works/{Guid.NewGuid():N}",
            Title: title,
            Authors: authors.Select(a => new OpenLibraryAuthor("/authors/OL1A", a.name, a.primary)).ToList(),
            FirstPublishYear: null,
            CoverId: null,
            Subjects: []);

    [Fact]
    public void PdfExample_Dickens_TaleTwoCities_Tier1()
    {
        var hierarchy = BuildHierarchy();
        var works = new List<OpenLibraryWork>
        {
            Work("A Tale of Two Cities", ("Charles Dickens", true))
        };
        var hypothesis = new ExtractedHypothesis("a tale of two cities", "dickens", [], null);

        var ranked = hierarchy.Rank(works, hypothesis);

        Assert.NotEmpty(ranked);
        Assert.Equal(MatchTier.Tier1ExactTitlePrimaryAuthor, ranked[0].Match.Tier);
    }

    [Fact]
    public void PdfExample_Tolkien_Hobbit_Tier1()
    {
        var hierarchy = BuildHierarchy();
        var works = new List<OpenLibraryWork>
        {
            Work("The Hobbit", ("J.R.R. Tolkien", true))
        };
        var hypothesis = new ExtractedHypothesis("the hobbit", "tolkien", ["illustrated", "deluxe"], 1937);

        var ranked = hierarchy.Rank(works, hypothesis);

        Assert.NotEmpty(ranked);
        Assert.Equal(MatchTier.Tier1ExactTitlePrimaryAuthor, ranked[0].Match.Tier);
    }

    [Fact]
    public void PdfExample_MarkHuckleberry_Tier4Fallback()
    {
        var hierarchy = BuildHierarchy();
        var works = new List<OpenLibraryWork>
        {
            Work("Adventures of Huckleberry Finn", ("Mark Twain", true)),
            Work("The Adventures of Tom Sawyer", ("Mark Twain", true))
        };
        var hypothesis = new ExtractedHypothesis(null, "mark twain", ["huckleberry"], null);

        var ranked = hierarchy.Rank(works, hypothesis);

        Assert.All(ranked, r => Assert.Equal(MatchTier.Tier4AuthorOnly, r.Match.Tier));
    }

    [Fact]
    public void RanksSortedByTierAscending()
    {
        var hierarchy = BuildHierarchy();
        var works = new List<OpenLibraryWork>
        {
            Work("Some Random Work", ("Nobody", true)),
            Work("The Hobbit", ("J.R.R. Tolkien", true))
        };
        var hypothesis = new ExtractedHypothesis("the hobbit", "tolkien", [], null);

        var ranked = hierarchy.Rank(works, hypothesis);

        Assert.Equal(2, ranked.Count);
        Assert.Equal(MatchTier.Tier1ExactTitlePrimaryAuthor, ranked[0].Match.Tier);
        Assert.Equal(MatchTier.Tier5NoWinner, ranked[1].Match.Tier);
    }

    [Fact]
    public void EmptyCandidates_ReturnsEmpty()
    {
        var hierarchy = BuildHierarchy();
        var hypothesis = new ExtractedHypothesis("the hobbit", "tolkien", [], null);

        var ranked = hierarchy.Rank([], hypothesis);

        Assert.Empty(ranked);
    }
}
