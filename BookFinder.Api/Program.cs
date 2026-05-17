using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using BookFinder.Application.Configuration;
using BookFinder.Application.Explanation;
using BookFinder.Application.Extraction;
using BookFinder.Application.Matching;
using BookFinder.Application.Normalization;
using BookFinder.Application.OpenLibrary;
using BookFinder.Application.Pipeline;
using BookFinder.Application.Ranking;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

// Bootstrap Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(outputTemplate: ctx.HostingEnvironment.IsDevelopment()
            ? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            : "{@t:o} {Level:u3} {Message:j} {Properties:j}{NewLine}{Exception}"));

    // --- Options ---
    builder.Services.Configure<OpenLibraryOptions>(builder.Configuration.GetSection("OpenLibrary"));
    builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection("AI"));
    builder.Services.Configure<FeaturesOptions>(builder.Configuration.GetSection("Features"));

    // --- Controllers + ProblemDetails ---
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // --- API versioning ---
    builder.Services.AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ReportApiVersions = true;
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

    // --- Swagger ---
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new() { Title = "BookFinder API", Version = "v1" });
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath)) o.IncludeXmlComments(xmlPath);
    });

    // --- Caching ---
    builder.Services.AddMemoryCache();
    builder.Services.AddOutputCache(o =>
    {
        o.AddPolicy("SearchCache", p =>
        {
            p.Expire(TimeSpan.FromMinutes(5));
            p.VaryByValue(ctx =>
            {
                // Cache is keyed on the raw query body — read synchronously from buffered body
                ctx.Request.EnableBuffering();
                using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
                var body = reader.ReadToEndAsync().GetAwaiter().GetResult();
                ctx.Request.Body.Position = 0;
                return new KeyValuePair<string, string>("body", body);
            });
        });
    });

    // --- Rate limiting ---
    var rl = builder.Configuration.GetSection("RateLimit");
    builder.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("search", limiterOpts =>
        {
            limiterOpts.PermitLimit = rl.GetValue("PermitPerWindow", 10);
            limiterOpts.Window = TimeSpan.FromSeconds(rl.GetValue("WindowSeconds", 60));
            limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOpts.QueueLimit = 0;
        });
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // --- Health checks ---
    var olBaseUrl = builder.Configuration["OpenLibrary:BaseUrl"] ?? "https://openlibrary.org";
    builder.Services.AddHealthChecks()
        .AddCheck("live", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
        .AddUrlGroup(new Uri($"{olBaseUrl}/"), name: "openlibrary", tags: ["ready"]);

    // --- Open Library HTTP client ---
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
    //   1. Set "AI:Provider" in appsettings.json to the desired value.
    //   2. Uncomment the relevant case below and add the corresponding NuGet package.
    //
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

    // --- Application services ---
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

    // -------------------------------------------------------------------------
    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "BookFinder v1"));
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseOutputCache();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => !check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready") || check.Name == "live"
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
