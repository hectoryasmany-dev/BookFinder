namespace BookFinder.Application.Configuration;

public sealed class FeaturesOptions
{
    public LlmReRankingOptions LlmReRanking { get; set; } = new();
}

public sealed class LlmReRankingOptions
{
    public bool Enabled { get; set; } = true;
}
