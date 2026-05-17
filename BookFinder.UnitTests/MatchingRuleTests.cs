using BookFinder.Application.Matching;
using BookFinder.Application.Models;
using BookFinder.Application.Normalization;

namespace BookFinder.UnitTests;

public class MatchingRuleTests
{
    private readonly TextNormalizer _normalizer = new();

    private static OpenLibraryWork Work(string title, params (string name, bool primary)[] authors)
        => new(
            WorkKey: "/works/OL1W",
            Title: title,
            Authors: authors.Select(a => new OpenLibraryAuthor("/authors/OL1A", a.name, a.primary)).ToList(),
            FirstPublishYear: null,
            CoverId: null,
            Subjects: []);

    private static ExtractedHypothesis Hypo(string? title = null, string? author = null)
        => new(title, author, [], null);

    // --- Tier1 ---

    [Fact]
    public void Tier1_ExactTitleAndPrimaryAuthor_Matches()
    {
        var rule = new Tier1ExactTitlePrimaryAuthorRule(_normalizer);
        var work = Work("A Tale of Two Cities", ("Charles Dickens", true));
        var hypo = Hypo("a tale of two cities", "dickens");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
        Assert.Equal(MatchTier.Tier1ExactTitlePrimaryAuthor, match!.Tier);
    }

    [Fact]
    public void Tier1_ExactTitle_NoAuthorInHypo_StillMatches()
    {
        var rule = new Tier1ExactTitlePrimaryAuthorRule(_normalizer);
        var work = Work("The Hobbit", ("J.R.R. Tolkien", true));
        var hypo = Hypo("the hobbit");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
    }

    [Fact]
    public void Tier1_WrongTitle_NoMatch()
    {
        var rule = new Tier1ExactTitlePrimaryAuthorRule(_normalizer);
        var work = Work("The Hobbit", ("J.R.R. Tolkien", true));
        var hypo = Hypo("lord of the rings", "tolkien");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    [Fact]
    public void Tier1_CorrectTitle_WrongPrimaryAuthor_NoMatch()
    {
        var rule = new Tier1ExactTitlePrimaryAuthorRule(_normalizer);
        var work = Work("The Hobbit", ("J.R.R. Tolkien", true));
        var hypo = Hypo("the hobbit", "dickens");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    // --- Tier2 ---

    [Fact]
    public void Tier2_ExactTitle_ContributorMatch_Matches()
    {
        var rule = new Tier2ExactTitleContributorRule(_normalizer);
        var work = Work("The Hobbit",
            ("J.R.R. Tolkien", true),
            ("Alan Lee", false));
        var hypo = Hypo("the hobbit", "alan lee");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
        Assert.Equal(MatchTier.Tier2ExactTitleContributor, match!.Tier);
        Assert.True(match.ContributorMatch);
    }

    [Fact]
    public void Tier2_ExactTitle_PrimaryAuthorOnly_NoMatch()
    {
        var rule = new Tier2ExactTitleContributorRule(_normalizer);
        var work = Work("The Hobbit", ("J.R.R. Tolkien", true));
        var hypo = Hypo("the hobbit", "tolkien");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    [Fact]
    public void Tier2_NoAuthorInHypo_NoMatch()
    {
        var rule = new Tier2ExactTitleContributorRule(_normalizer);
        var work = Work("The Hobbit", ("J.R.R. Tolkien", true));
        var hypo = Hypo("the hobbit");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    // --- Tier3 ---

    [Fact]
    public void Tier3_NearMatchTitle_AuthorMatch_Matches()
    {
        var rule = new Tier3NearMatchTitleAuthorRule(_normalizer);
        var work = Work("Great Expectations", ("Charles Dickens", true));
        var hypo = Hypo("great expectation", "dickens");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
        Assert.Equal(MatchTier.Tier3NearMatchTitleAuthor, match!.Tier);
        Assert.True(match.SimilarityScore >= 0.85);
    }

    [Fact]
    public void Tier3_NearMatchTitle_NoAuthorInHypo_Matches()
    {
        var rule = new Tier3NearMatchTitleAuthorRule(_normalizer);
        var work = Work("Great Expectations", ("Charles Dickens", true));
        var hypo = Hypo("great expectation");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
    }

    [Fact]
    public void Tier3_TitleBelowThreshold_NoMatch()
    {
        var rule = new Tier3NearMatchTitleAuthorRule(_normalizer);
        var work = Work("Great Expectations", ("Charles Dickens", true));
        var hypo = Hypo("lord of the rings", "tolkien");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    // --- Tier4 ---

    [Fact]
    public void Tier4_AuthorMatch_Matches()
    {
        var rule = new Tier4AuthorOnlyRule(_normalizer);
        var work = Work("Oliver Twist", ("Charles Dickens", true));
        var hypo = Hypo(author: "dickens");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
        Assert.Equal(MatchTier.Tier4AuthorOnly, match!.Tier);
    }

    [Fact]
    public void Tier4_NoAuthorInHypo_NoMatch()
    {
        var rule = new Tier4AuthorOnlyRule(_normalizer);
        var work = Work("Oliver Twist", ("Charles Dickens", true));
        var hypo = Hypo("oliver twist");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    [Fact]
    public void Tier4_AuthorMismatch_NoMatch()
    {
        var rule = new Tier4AuthorOnlyRule(_normalizer);
        var work = Work("Oliver Twist", ("Charles Dickens", true));
        var hypo = Hypo(author: "tolkien");

        var match = rule.TryMatch(work, hypo);

        Assert.Null(match);
    }

    // --- Tier5 ---

    [Fact]
    public void Tier5_AlwaysMatches()
    {
        var rule = new Tier5NoWinnerRule();
        var work = Work("Unknown Book");
        var hypo = Hypo("something random");

        var match = rule.TryMatch(work, hypo);

        Assert.NotNull(match);
        Assert.Equal(MatchTier.Tier5NoWinner, match!.Tier);
    }
}
