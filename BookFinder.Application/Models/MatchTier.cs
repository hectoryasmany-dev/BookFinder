namespace BookFinder.Application.Models;

public enum MatchTier
{
    Tier1ExactTitlePrimaryAuthor = 1,
    Tier2ExactTitleContributor   = 2,
    Tier3NearMatchTitleAuthor    = 3,
    Tier4AuthorOnly              = 4,
    Tier5NoWinner                = 5
}
