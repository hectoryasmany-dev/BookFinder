using BookFinder.Application.Normalization;

namespace BookFinder.UnitTests;

public class TextNormalizerTests
{
    private readonly TextNormalizer _sut = new();

    [Fact]
    public void Normalize_LowercasesInput()
    {
        var result = _sut.Normalize("Hello World");
        Assert.Equal("hello world", result.Normalized);
    }

    [Fact]
    public void Normalize_StripsDiacritics()
    {
        var result = _sut.Normalize("Ñoño Über café");
        Assert.Equal("nono uber cafe", result.Normalized);
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        var result = _sut.Normalize("  too   many   spaces  ");
        Assert.Equal("too many spaces", result.Normalized);
    }

    [Fact]
    public void Normalize_ReturnsTokens()
    {
        var result = _sut.Normalize("tolkien hobbit 1937");
        Assert.Equal(new[] { "tolkien", "hobbit", "1937" }, result.Tokens);
    }

    [Fact]
    public void NormalizeTitle_StripsSubtitleAfterColon()
    {
        var result = _sut.NormalizeTitle("A Tale of Two Cities: A Historical Novel");
        Assert.DoesNotContain("historical novel", result);
        Assert.Contains("tale of two cities", result);
    }

    [Fact]
    public void NormalizeTitle_StripsSubtitleAfterEmDash()
    {
        var result = _sut.NormalizeTitle("The Hobbit — There and Back Again");
        Assert.DoesNotContain("there and back again", result);
    }

    [Fact]
    public void NormalizeTitle_StripsLeadingArticle_The()
    {
        var result = _sut.NormalizeTitle("The Lord of the Rings");
        Assert.Equal("lord of the rings", result);
    }

    [Fact]
    public void NormalizeTitle_StripsLeadingArticle_A()
    {
        var result = _sut.NormalizeTitle("A Tale of Two Cities");
        Assert.Equal("tale of two cities", result);
    }

    [Fact]
    public void NormalizeTitle_StripsLeadingArticle_An()
    {
        var result = _sut.NormalizeTitle("An Inspector Calls");
        Assert.Equal("inspector calls", result);
    }

    [Fact]
    public void NormalizeTitle_DoesNotStripNonLeadingArticle()
    {
        var result = _sut.NormalizeTitle("Lord of the Rings");
        Assert.Equal("lord of the rings", result);
    }
}
