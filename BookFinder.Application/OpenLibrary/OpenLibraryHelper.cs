namespace BookFinder.Application.OpenLibrary;

public static class OpenLibraryHelper
{
    private const string CoverBaseUrl = "https://covers.openlibrary.org/b/id";
    private const string WorkBaseUrl  = "https://openlibrary.org";

    public static string CoverUrl(int coverId) =>
        $"{CoverBaseUrl}/{coverId}-M.jpg";

    public static string WorkUrl(string workKey) =>
        $"{WorkBaseUrl}{workKey}";
}