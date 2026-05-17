using System.Text.Json.Serialization;

namespace BookFinder.Application.OpenLibrary;

internal sealed class OlSearchResponse
{
    [JsonPropertyName("docs")]
    public List<OlSearchDoc> Docs { get; set; } = [];
}

internal sealed class OlSearchDoc
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author_name")]
    public List<string>? AuthorName { get; set; }

    [JsonPropertyName("author_key")]
    public List<string>? AuthorKey { get; set; }

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; set; }

    [JsonPropertyName("subject")]
    public List<string>? Subject { get; set; }
}

internal sealed class OlWorkDetail
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<OlWorkAuthorEntry>? Authors { get; set; }
}

internal sealed class OlWorkAuthorEntry
{
    [JsonPropertyName("author")]
    public OlKeyRef? Author { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

internal sealed class OlKeyRef
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}

internal sealed class OlAuthorDetail
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
