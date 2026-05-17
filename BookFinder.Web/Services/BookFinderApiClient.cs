using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookFinder.Web.Models;

namespace BookFinder.Web.Services;

public sealed class BookFinderApiClient(HttpClient http)
{
    public Uri? BaseAddress => http.BaseAddress;
    public async Task<SearchBookResponse?> SearchAsync(string query, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/v1/books/search", new SearchBookRequest(query), ct);

        if (!response.IsSuccessStatusCode)
        {
            // Read ProblemDetails.detail so the UI can show the API's actual error message
            string? detail = null;
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                detail = problem.TryGetProperty("detail", out var d) ? d.GetString() : null;
            }
            catch { /* ignore parse failures — fall through to status-only message */ }

            throw new HttpRequestException(
                detail ?? response.ReasonPhrase,
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<SearchBookResponse>(ct);
    }
}
