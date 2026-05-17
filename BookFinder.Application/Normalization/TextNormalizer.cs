using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BookFinder.Application.Models;

namespace BookFinder.Application.Normalization;

public sealed class TextNormalizer
{
    private static readonly HashSet<string> LeadingArticles =
        new(StringComparer.OrdinalIgnoreCase) { "the", "a", "an" };

    private static readonly Regex SubtitlePattern =
        new(@"\s*(:|—|-{2})\s+.*$", RegexOptions.Compiled);

    private static readonly Regex CollapseWhitespace =
        new(@"\s+", RegexOptions.Compiled);

    public NormalizedQuery Normalize(string raw)
    {
        var normalized = NormalizeCore(raw);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new NormalizedQuery(raw, normalized, tokens);
    }

    public string NormalizeTitle(string title, bool stripSubtitle = true)
    {
        var s = title;
        if (stripSubtitle)
            s = SubtitlePattern.Replace(s, string.Empty);
        s = NormalizeCore(s);
        s = StripLeadingArticle(s);
        return s;
    }

    public string NormalizeAuthor(string author) => NormalizeCore(author);

    private static string NormalizeCore(string input)
    {
        var decomposed = input.Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                stripped.Append(ch);
        }

        var lower = stripped.ToString().ToLowerInvariant();
        return CollapseWhitespace.Replace(lower.Trim(), " ");
    }

    private static string StripLeadingArticle(string s)
    {
        foreach (var article in LeadingArticles)
        {
            if (s.StartsWith(article + " ", StringComparison.Ordinal))
                return s[(article.Length + 1)..];
        }
        return s;
    }
}
