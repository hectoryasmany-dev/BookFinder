namespace BookFinder.Application.Models;

public sealed record OpenLibraryAuthor(
    string AuthorKey,
    string Name,
    bool IsPrimaryAuthor);
