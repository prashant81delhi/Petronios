# Santander Hacker News API

Production-oriented ASP.NET Core 8 API exposing Hacker News best stories.

## Run

Start Redis locally (for example `docker run --rm -p 6379:6379 redis:7-alpine`), then:

```bash
dotnet restore
dotnet run --project SantanderHackerNews.Api
```

GET `/api/stories/best?n=10`; `n` must be between 1 and 100. Use `&refresh=true` to force a refresh. Swagger is available at `/swagger`.
Configuration is in `appsettings.json` and supports environment overrides such as
`ConnectionStrings__Redis`.

Redis provides distributed caching. `IHttpClientFactory` uses standard resilience policies
(timeouts, exponential jittered retry, circuit breaker), while bounded concurrent item requests
provide outbound bulkhead protection. The service coalesces concurrent refreshes and runs a bounded
periodic background refresh. ASP.NET Core fixed-window inbound rate limiting protects the endpoint.
Stories are fetched and ranked by score; `uri` is the Hacker News `url` and is null when the
upstream story has no URL. `time` is serialized as UTC ISO-8601 and `commentCount` uses
Hacker News `descendants`.

## Test

`dotnet test` runs isolated tests using an in-memory distributed cache and fake HTTP handlers;
no Redis or internet access is required.
