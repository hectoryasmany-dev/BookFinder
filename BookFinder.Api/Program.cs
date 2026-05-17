using BookFinder.Application.Configuration;
using BookFinder.Application.OpenLibrary;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<OpenLibraryOptions>(
    builder.Configuration.GetSection("OpenLibrary"));

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>(client =>
{
    var baseUrl = builder.Configuration["OpenLibrary:BaseUrl"] ?? "https://openlibrary.org";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "BookFinder/1.0");
})
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    options.Retry.MaxRetryAttempts = 3;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
