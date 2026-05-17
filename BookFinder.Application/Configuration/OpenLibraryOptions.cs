namespace BookFinder.Application.Configuration;

public sealed class OpenLibraryOptions
{
    public string BaseUrl { get; set; } = "https://openlibrary.org";
    public int CacheMinutes { get; set; } = 60;
}
