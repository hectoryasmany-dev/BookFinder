# BookFinder

A .NET 8 book-search application that accepts free-text queries ("tolkien hobbit illustrated deluxe 1937") and returns up to five ranked candidates from the [Open Library](https://openlibrary.org) catalogue.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Project Structure](#3-project-structure)
4. [Prerequisites](#4-prerequisites)
5. [API Key Setup](#5-api-key-setup)
6. [Running Locally](#6-running-locally)
7. [Running with Docker](#7-running-with-docker)
8. [Configuration](#8-configuration)
9. [API Reference](#9-api-reference)
10. [Design Decisions](#10-design-decisions)
11. [Testing](#11-testing)
12. [Features](#12-features)

---

## 1. Overview

Users type a messy, free-text description of a book. BookFinder:

1. Normalises the query (diacritics, casing, articles).
2. Uses an LLM (Google Gemini via `Microsoft.Extensions.AI`) to extract a structured hypothesis — title, author, year, keywords.
3. Searches Open Library for candidates.
4. Runs a deterministic five-tier matching hierarchy to score and rank them.
5. Optionally re-ranks the top results with a second LLM call.
6. Returns up to five results with cover images, publication years, Open Library links, and a plain-English explanation of why each result was selected.

---

## 2. Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  BookFinder.Web  (Blazor WASM — served by nginx)               │
│  Single page: query input → result cards                        │
└────────────────────────┬────────────────────────────────────────┘
                         │  POST /api/v1/books/search
                         │  (nginx reverse-proxies in Docker)
┌────────────────────────▼────────────────────────────────────────┐
│  BookFinder.Api  (ASP.NET Core 8 — REST controller)             │
│  Versioning · FluentValidation · OutputCache · RateLimiter      │
└────────────────────────┬────────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────────┐
│  BookFinder.Application  (domain library)                       │
│                                                                 │
│  normalize → [LLM extract] → OL search → deduplicate           │
│           → match hierarchy → [LLM re-rank] → explain          │
│                                                                 │
│  LLM calls use IChatClient (MEAI); Google.GenAI is the         │
│  concrete provider wired in BookFinder.Api/Program.cs.         │
└─────────────────────────────────────────────────────────────────┘
```

**Key principle:** LLM only at the edges. Extraction and optional re-ranking use the model; everything in between is deterministic C#.

---

## 3. Project Structure

```
BookFinder.Application/       Domain logic — no web/DI framework deps
  Configuration/              Options classes (AI, OpenLibrary, Features)
  Extraction/                 ILlmExtractor, LlmExtractor, FakeLlmExtractor
  Matching/                   Five IMatchRule implementations + MatchingHierarchy
  Normalization/              TextNormalizer (diacritics, articles, token matching)
  OpenLibrary/                IOpenLibraryClient, OpenLibraryClient (cached)
  Pipeline/                   SearchPipeline — orchestrates all stages
  Ranking/                    ILlmReRanker, LlmReRanker, NoOpReRanker
  Explanation/                ExplanationBuilder — human-readable match rationale

BookFinder.Api/               ASP.NET Core Web API
  Controllers/BooksController.cs
  Contracts/                  Request/response records
  Mapping/                    SearchResultMapper (domain → DTO)
  Validation/                 FluentValidation validators

BookFinder.Web/               Blazor WASM client
  Pages/Home.razor            Single search page
  Services/BookFinderApiClient.cs
  Models/SearchModels.cs
  wwwroot/                    index.html (Bootstrap 5), appsettings*.json

BookFinder.UnitTests/         xUnit unit tests (43 tests)
BookFinder.IntegrationTests/  xUnit integration tests via WebApplicationFactory (9 tests)
```

---

## 4. Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | any recent |
| Google Gemini API key | [aistudio.google.com](https://aistudio.google.com) |

---

## 5. API Key Setup

BookFinder requires a Google Gemini API key. Get one free at [aistudio.google.com](https://aistudio.google.com).

**Never commit your key.** `appsettings.json` intentionally has an empty value for `AI:GeminiApiKey`.

### Local dev — .NET User Secrets (recommended)

```powershell
cd BookFinder.Api
dotnet user-secrets set "AI:GeminiApiKey" "your-key-here"
```

User secrets are stored outside the repo and loaded automatically in the `Development` environment.

### Docker — `.env` file

Create a `.env` file at the **solution root** (already in `.gitignore`):

```
GEMINI_API_KEY=your-key-here
```

`docker compose` picks this up automatically and maps it to `AI__GeminiApiKey` inside the container.

---

## 6. Running Locally

### API

```powershell
cd BookFinder.Api
dotnet run --launch-profile http
# Swagger: http://localhost:5221/swagger
```

### Blazor Web

```powershell
cd BookFinder.Web
dotnet run --launch-profile http
# UI: http://localhost:5058
```

Both projects must use the `http` launch profile so ports are stable and predictable. The Web project reads `ApiBaseUrl` from `wwwroot/appsettings.json` — defaults to `http://localhost:5221`.

---

## 7. Running with Docker

Ensure you have created the `.env` file as described in [API Key Setup](#5-api-key-setup), then:

```powershell
# From the solution root
docker compose up --build
```

| Service | URL |
|---------|-----|
| Web UI  | http://localhost |
| API     | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |

nginx reverse-proxies `/api/` to the API container — the browser sees a single origin, no CORS preflight required.

```powershell
docker compose down   # stop and remove containers
```

---

## 8. Configuration

All settings live in `BookFinder.Api/appsettings.json`. Override per environment or via environment variables (double-underscore for nested keys, e.g. `AI__GeminiApiKey`).

| Key | Default | Description |
|-----|---------|-------------|
| `AI:Provider` | `Gemini` | LLM provider — see [Switching LLM Provider](#switching-llm-provider) |
| `AI:GeminiApiKey` | _(empty)_ | Google Gemini API key — **set via user secrets or `.env`, never commit a real key** |
| `AI:GeminiModel` | `gemini-2.5-flash` | Gemini model name |
| `AI:LlmTimeoutSeconds` | `60` | Hard cap per LLM call; on timeout the pipeline falls back to normalised-query matching gracefully |
| `OpenLibrary:BaseUrl` | `https://openlibrary.org` | Override for testing |
| `OpenLibrary:CacheMinutes` | `60` | TTL for OL search and author detail caches |
| `Features:LlmReRanking:Enabled` | `false` | Enable optional second LLM pass for re-ranking |
| `RateLimit:PermitPerWindow` | `10` | Requests per window |
| `RateLimit:WindowSeconds` | `60` | Rate-limit window |
| `Cors:AllowedOrigins` | _(array)_ | Origins allowed in local dev; not needed in Docker (nginx proxies) |

---

## 9. API Reference

### `POST /api/v1/books/search`

**Request**

```json
{ "query": "tolkien hobbit illustrated deluxe 1937" }
```

**Response 200**

```json
{
  "results": [
    {
      "title": "The Hobbit",
      "author": "J.R.R. Tolkien",
      "firstPublishYear": 1937,
      "explanation": "Exact title match with primary author.",
      "coverImageUrl": "https://covers.openlibrary.org/b/id/12345-M.jpg",
      "openLibraryWorkId": "/works/OL262758W",
      "openLibraryUrl": "https://openlibrary.org/works/OL262758W"
    }
  ]
}
```

**Error responses**

| Status | Condition |
|--------|-----------|
| 400 | Empty or missing query |
| 429 | Rate limit exceeded |
| 502 | Open Library unreachable |

**Health endpoints**

```
GET /health/live   — liveness (always 200 if process is up)
GET /health/ready  — readiness (includes Open Library reachability)
```

---

## 10. Design Decisions

### LLM only at the edges

The LLM runs twice at most: once to extract a structured hypothesis from the raw query, and optionally once to re-rank the top candidates. Everything in between — searching, deduplicating, matching, scoring — is deterministic C#. This keeps the pipeline fast, cheap, testable, and auditable. A broken LLM key degrades gracefully (the pipeline falls back to using the raw tokenised query).

### Five-tier matching hierarchy

Rather than a single similarity score, candidates are classified into one of five tiers:

| Tier | Rule |
|------|------|
| 1 | Exact title + primary author |
| 2 | Exact title + any contributor |
| 3 | Near-match title (JaroWinkler ≥ 0.85) + author |
| 4 | Author only |
| 5 | No winner — included for completeness |

The first tier a work qualifies for wins. This produces predictable, explainable results — each result carries a plain-English explanation built from the tier logic rather than an opaque confidence score.

### `Result<T>` over exceptions for expected failures

`OpenLibraryClient` returns `Result<T>` instead of throwing on HTTP failures. This makes the "Open Library is down" path a first-class value that flows up to the controller and becomes a `502` — not a `500` from an unhandled exception.

### `Microsoft.Extensions.AI` as the LLM abstraction

`IChatClient` is the only LLM type referenced in `BookFinder.Application`. The concrete provider is wired once in `BookFinder.Api/Program.cs` via `.AsIChatClient()`. `LlmExtractor` and `LlmReRanker` have zero knowledge of which model they're talking to.

#### Switching LLM Provider

Change `AI:Provider` in `appsettings.json` and update the DI registration in `Program.cs`. Commented stubs are already in place:

| Provider | `AI:Provider` value | NuGet package |
|---|---|---|
| Google Gemini _(default)_ | `Gemini` | `Google.GenAI` |
| Azure OpenAI | `AzureOpenAI` | `Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI` |
| OpenAI | `OpenAI` | `Microsoft.Extensions.AI.OpenAI` |

No changes required outside `Program.cs` — the entire Application layer is provider-agnostic by design.

### Open Library client caching

`IMemoryCache` with a one-hour TTL caches search results keyed on the extracted hypothesis fields. Repeated identical queries never hit the Open Library API twice. Work detail enrichment uses `SemaphoreSlim(5)` + `Task.WhenAll` to parallelise up to five concurrent requests.

### Blazor WASM + nginx reverse proxy

In Docker, nginx serves the compiled WASM static files and proxies `/api/` to the API container. From the browser's perspective both the UI and the API share the same origin — no CORS preflight needed. In local development, `ApiBaseUrl` in `wwwroot/appsettings.json` points directly at the API port.

### OutputCache at the API layer

`SearchCache` policy caches identical POSTs for five minutes, keyed on the raw request body. A high-traffic user asking for "tolkien hobbit" repeatedly hits the cache, not the LLM or Open Library.

### No service locator, no static state

All services are injected. `SearchPipeline` holds no mutable state — it can safely be registered as a singleton and called concurrently.

---

## 11. Testing

```powershell
dotnet test          # runs all 52 tests
dotnet test --filter "FullyQualifiedName~UnitTests"         # unit only (43)
dotnet test --filter "FullyQualifiedName~IntegrationTests"  # integration only (9)
```

### Unit tests (`BookFinder.UnitTests`)

Cover `TextNormalizer`, all five match rules, `MatchingHierarchy`, `ExplanationBuilder`, `SearchPipeline`, and `OpenLibraryClient`. LLM calls are replaced by `FakeLlmExtractor` — a simple test double, not a mock of `IChatClient`.

### Integration tests (`BookFinder.IntegrationTests`)

Spin up the full ASP.NET Core pipeline via `WebApplicationFactory<Program>`. `ILlmExtractor` and `IOpenLibraryClient` are overridden with NSubstitute fakes; `IChatClient` is stubbed and `ILlmReRanker` is replaced with `NoOpReRanker` so no real Gemini key is needed. Tests cover happy-path 200, validation 400s, 502 upstream failure, and cancellation propagation.

---

## 12. Features

- [x] Free-text book search via Open Library
- [x] LLM extraction (Google Gemini) with typo correction and structured JSON output
- [x] Five-tier deterministic matching hierarchy
- [x] Graceful LLM fallback — returns results even without a Gemini key
- [x] Configurable LLM timeout with graceful degradation
- [x] Optional LLM re-ranking (feature-flagged, off by default)
- [x] Swappable LLM provider via `AI:Provider` config — zero application-layer changes
- [x] Cover images, publication years, Open Library links
- [x] Plain-English match explanation per result
- [x] FluentValidation + RFC 7807 ProblemDetails error responses
- [x] Output caching (5 min, keyed on request body)
- [x] Rate limiting (10 req/min fixed window)
- [x] Health checks (`/health/live`, `/health/ready`)
- [x] API versioning (`/api/v1/`)
- [x] Swagger / OpenAPI
- [x] Serilog structured logging with per-stage timings
- [x] Docker multi-stage builds + nginx reverse proxy
- [x] 52 tests (43 unit + 9 integration)

---

## 13. Future Improvements

**Adaptive Matching via Relevance Feedback** — Let users mark "this was it" or "none of these" to gradually re-weight the matching hierarchy, producing a labelled dataset and a system that measurably improves with use.

**Semantic Search via Embeddings** — Replace Jaro-Winkler fuzzy matching with cosine similarity over Gemini `gemini-2.5-flash` embeddings, making conceptual queries like *"dystopian pig farm allegory"* resolve correctly where character-level matching cannot.

**Personal Library & Reading Lists** — Let users curate *want to read / already read* lists via `localStorage` (or a lightweight SQL backend for cross-device sync), laying the data foundation for future collaborative filtering.

**Resilience Policies with Polly** — Wrap the Open Library `HttpClient` with `AddResilienceHandler` (Polly v8) for exponential back-off retries, a circuit breaker, and a timeout policy — production-grade HTTP hygiene in ~10 lines of DI config.

**.NET Aspire Orchestration** — Replace `docker-compose.yml` with an Aspire AppHost for automatic service discovery, a built-in OpenTelemetry dashboard, and a clear one-command path to Azure Container Apps deployment.

**Distributed Tracing with OpenTelemetry** — Replace per-stage Serilog timing logs with `Activity` spans so the full pipeline trace — extract → search → enrich → match — is queryable in any OTLP-compatible backend such as Jaeger or Azure Monitor.
