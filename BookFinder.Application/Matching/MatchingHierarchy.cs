using BookFinder.Application.Models;

namespace BookFinder.Application.Matching;

public sealed class MatchingHierarchy
{
    private readonly IEnumerable<IMatchRule> _rules;

    public MatchingHierarchy(IEnumerable<IMatchRule> rules) => _rules = rules;

    public IReadOnlyList<RankedCandidate> Rank(
        IReadOnlyList<OpenLibraryWork> candidates,
        ExtractedHypothesis hypothesis)
    {
        var ranked = new List<(OpenLibraryWork Work, MatchDetails Match)>();

        foreach (var work in candidates)
        {
            foreach (var rule in _rules)
            {
                var match = rule.TryMatch(work, hypothesis);
                if (match is not null)
                {
                    ranked.Add((work, match));
                    break;
                }
            }
        }

        return ranked
            .OrderBy(r => (int)r.Match.Tier)
            .Select(r => new RankedCandidate(r.Work, r.Match, string.Empty))
            .ToList();
    }
}
