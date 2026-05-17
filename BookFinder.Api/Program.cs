using BookFinder.Application.Configuration;
using BookFinder.Application.Explanation;
using BookFinder.Application.Extraction;
using BookFinder.Application.Matching;
using BookFinder.Application.Normalization;
using BookFinder.Application.OpenLibrary;
using BookFinder.Application.Pipeline;
using BookFinder.Application.Ranking;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<OpenLibraryOptions>(builder.Configuration.GetSection("OpenLibrary"));
builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection("AI"));
builder.Services.Configure<FeaturesOptions>(builder.Configuration.GetSection("Features"));

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

// ---------------------------------------------------------------------------
// AI Provider Registration
// ---------------------------------------------------------------------------
// LlmExtractor and LlmReRanker depend only on IChatClient (Microsoft.Extensions.AI),
// so swapping providers requires no changes to application logic — only this factory.
//
// To switch providers:
//   1. Set "AI:Provider" in appsettings.json (or user secrets / env var) to the desired value.
//   2. Uncomment the relevant case below and add the corresponding NuGet package.
//   3. Add the required config keys to AiProviderOptions and appsettings.json.
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;

    return opts.Provider switch
    {
        "Gemini" => new Google.GenAI.Client(apiKey: opts.GeminiApiKey)
                        .AsIChatClient(opts.GeminiModel),

        // "AzureOpenAI" => new Azure.AI.OpenAI.AzureOpenAIClient(
        //     new Uri(opts.AzureOpenAiEndpoint),
        //     new Azure.AzureKeyCredential(opts.AzureOpenAiApiKey))
        //         .AsIChatClient(opts.AzureOpenAiDeploymentName),
        // requires: dotnet add package Azure.AI.OpenAI
        //           dotnet add package Microsoft.Extensions.AI.OpenAI

        _ => throw new InvalidOperationException(
            $"Unknown AI provider '{opts.Provider}'. Valid values: Gemini, AzureOpenAI.")
    };
});

builder.Services.AddSingleton<TextNormalizer>();
builder.Services.AddSingleton<Deduplicator>();
builder.Services.AddSingleton<ExplanationBuilder>();
builder.Services.AddSingleton<IMatchRule, Tier1ExactTitlePrimaryAuthorRule>();
builder.Services.AddSingleton<IMatchRule, Tier2ExactTitleContributorRule>();
builder.Services.AddSingleton<IMatchRule, Tier3NearMatchTitleAuthorRule>();
builder.Services.AddSingleton<IMatchRule, Tier4AuthorOnlyRule>();
builder.Services.AddSingleton<IMatchRule, Tier5NoWinnerRule>();
builder.Services.AddSingleton<MatchingHierarchy>();
builder.Services.AddSingleton<ILlmExtractor, LlmExtractor>();
builder.Services.AddSingleton<ISearchPipeline, SearchPipeline>();

var features = builder.Configuration.GetSection("Features:LlmReRanking").Get<LlmReRankingOptions>()
    ?? new LlmReRankingOptions();

if (features.Enabled)
    builder.Services.AddSingleton<ILlmReRanker, LlmReRanker>();
else
    builder.Services.AddSingleton<ILlmReRanker, NoOpReRanker>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
