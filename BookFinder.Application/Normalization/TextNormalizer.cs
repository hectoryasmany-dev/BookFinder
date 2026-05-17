using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BookFinder.Application.Models;

namespace BookFinder.Application.Normalization;

public sealed class TextNormalizer
{
    private static readonly string[] LeadingArticles = ["the", "a", "an"];

    // Matches ": subtitle" or " — subtitle" (em-dash)
    private static readonly Regex SubtitlePattern =
        new(@"(\s*:|\s+—)\s*.*$", RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern =
        new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes raw input into a NormalizedQuery (no subtitle strip, no article strip).
    /// </summary>
    public NormalizedQuery Normalize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var normalized = ApplyCore(raw, stripSubtitle: false);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new NormalizedQuery(raw, normalized, tokens);
    }

    /// <summary>
    /// Normalizes a title for exact/fuzzy comparison (strips subtitle + leading article).
    /// </summary>
    public string NormalizeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var core = ApplyCore(title, stripSubtitle: true);
        return StripLeadingArticle(core);
    }

    /// <summary>
    /// Normalizes an author name for comparison (lowercase + diacritics only).
    /// </summary>
    public string NormalizeAuthor(string author)
    {
        ArgumentNullException.ThrowIfNull(author);
        var lower = author.ToLowerInvariant();
        var stripped = StripDiacritics(lower);
        return WhitespacePattern.Replace(stripped.Trim(), " ");
    }

    /// <summary>
    /// Returns true if workAuthor contains hypothesisAuthor or vice-versa
    /// (handles "J.R.R. Tolkien" matching "Tolkien").
    /// </summary>
    public bool AuthorMatches(string workAuthorName, string hypothesisAuthor)
    {
        var normWork = NormalizeAuthor(workAuthorName);
        var normHypo = NormalizeAuthor(hypothesisAuthor);

        // All tokens from the shorter (hypothesis) side must appear in the work author name.
        // "tolkien" → matches "j r r tolkien" ✓
        // "s"       → does NOT match "dickens" ✓
        var hypTokens = normHypo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var workTokens = normWork.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return hypTokens.All(t => workTokens.Contains(t))
            || workTokens.All(t => hypTokens.Contains(t));
    }

    private static string ApplyCore(string text, bool stripSubtitle)
    {
        var result = text.ToLowerInvariant();
        result = StripDiacritics(result);
        if (stripSubtitle)
            result = SubtitlePattern.Replace(result, string.Empty);
        result = WhitespacePattern.Replace(result.Trim(), " ");
        return result;
    }

    private static string StripLeadingArticle(string text)
    {
        foreach (var article in LeadingArticles)
        {
            if (text.StartsWith(article + " ", StringComparison.Ordinal))
                return text[(article.Length + 1)..];
        }
        return text;
    }

    private static string StripDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

}
