# Santander Hacker News API

Production-oriented ASP.NET Core 8 API exposing Hacker News best stories. The
API uses an in-process cache by default and can use Redis for distributed
caching when explicitly configured. Hacker News Firebase is the upstream data
source.

## Prerequisites

Install the following tools before starting:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git, to clone the repository

```bash
dotnet --version
```

The SDK version must be 8.x. Redis and Docker are optional for the default
local-cache mode.

## Optional: start Redis

To use Redis instead of the default in-process cache, start a Redis 7
container:

```bash
docker run -d --name santander-redis -p 6379:6379 redis:7-alpine
```

This publishes Redis on `localhost:6379`. Verify that the container is running and accepting
connections:

```bash
docker ps --filter "name=santander-redis"
docker exec santander-redis redis-cli ping
```

The second command should return `PONG`. If the container already exists,
start it instead of creating a second one:

```bash
docker start santander-redis
```

## Run the API

From the repository root:

```bash
dotnet restore
dotnet run --project SantanderHackerNews.Api
```

The HTTP launch profile listens on `http://localhost:5233`. Keep the terminal
running while using the API.

### Configuration and environment variables

Runtime configuration is loaded from `SantanderHackerNews.Api/appsettings.json`
and can be overridden with environment variables using the standard ASP.NET
Core double-underscore separator. For example:

```powershell
$env:Cache__Provider = "Redis"
$env:ConnectionStrings__Redis = "localhost:6379"
$env:HackerNews__StoryCount = "20"
$env:HackerNews__CacheSeconds = "60"
$env:HackerNews__TimeoutSeconds = "10"
$env:HackerNews__MaxConcurrentItemRequests = "8"
$env:HackerNews__OutboundRequestsPerSecond = "20"
$env:HackerNews__RefreshIntervalSeconds = "30"
dotnet run --project SantanderHackerNews.Api
```

The available settings are:

| Setting | Default | Purpose |
| --- | --- | --- |
| `Cache__Provider` | `Memory` | Cache backend: `Memory` or `Redis` |
| `ConnectionStrings__Redis` | *(not set)* | Redis connection string when `Cache__Provider=Redis` |
| `HackerNews__BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Hacker News API base URL |
| `HackerNews__StoryCount` | `20` | Number of stories refreshed by the background service |
| `HackerNews__CacheSeconds` | `60` | Fresh cache lifetime |
| `HackerNews__TimeoutSeconds` | `10` | Upstream HTTP client timeout |
| `HackerNews__MaxConcurrentItemRequests` | `8` | Maximum concurrent story-item requests |
| `HackerNews__OutboundRequestsPerSecond` | `20` | Outbound request-rate limit |
| `HackerNews__RefreshIntervalSeconds` | `30` | Background refresh interval |

`ASPNETCORE_ENVIRONMENT` can also be set to select an environment-specific
configuration file, such as `appsettings.Development.json`.

## API endpoints

Fetch the 10 highest-scoring best stories:

```text
GET http://localhost:5233/api/stories/best?n=10
```

Examples using curl:

```bash
curl "http://localhost:5233/api/stories/best?n=10"
curl "http://localhost:5233/api/stories/best?n=10&refresh=true"
```

`n` is optional and must be between 1 and 100; it defaults to 10.
`refresh=true` bypasses the cached response and requests a refresh. Stories
are fetched and ranked by score. `uri` is the Hacker News `url` and is null
when the upstream story has no URL. `time` is serialized as UTC ISO-8601 and
`commentCount` uses Hacker News `descendants`.

The endpoint is protected by an inbound fixed-window rate limit of 60 requests
per client IP per minute. A rejected request returns HTTP 429, and an invalid
`n` returns HTTP 400.

Operational endpoints are intentionally outside the client rate limit:

- `GET /health/live` checks that the process is running.
- `GET /health/ready` reports whether configured application health checks pass.

Each response includes an `X-Request-Id` header. Send the same header from a
caller to correlate its request with structured application logs.

## Swagger

With the API running, open
[http://localhost:5233/swagger](http://localhost:5233/swagger) to view and
execute the OpenAPI operations interactively.

## Tests

Run the full test suite from the repository root:

```bash
dotnet test --configuration Release
```

Tests use an in-memory distributed cache and fake HTTP handlers, so they do not
require Redis, Docker, or internet access.

## Docker and CI

Build and run the API with Redis using Docker Compose:

```bash
docker compose up --build
curl http://localhost:8080/health/ready
curl "http://localhost:8080/api/stories/best?n=10"
```

The multi-stage `Dockerfile` publishes a non-root ASP.NET Core image. The
GitHub Actions workflow restores, builds, tests with coverage collection, and
builds the container image for every pull request and push to `main`.

See [docs/architecture.md](docs/architecture.md) for service boundaries,
resilience decisions, scaling, persistence trade-offs, and testing strategy.

## Troubleshooting

### Running without Redis

No Redis installation or configuration is needed for local development. The
default `Cache:Provider` is `Memory`, which stores cache entries in the API
process. Entries are lost when the process stops and are not shared between
instances.

### Redis is not reachable

Redis is only used when `Cache:Provider=Redis`. If Redis is not reachable,
switch back to local mode:

```powershell
$env:Cache__Provider = "Memory"
dotnet run --project SantanderHackerNews.Api
```

Check the container and its logs:

```bash
docker ps -a --filter "name=santander-redis"
docker logs santander-redis
docker start santander-redis
docker exec santander-redis redis-cli ping
```

Confirm that `Cache__Provider` is `Redis` and the API's
`ConnectionStrings__Redis` value matches the published host and port. If port 6379 is already in use, publish another host port and
use the matching value, for example:

```bash
docker run -d --name santander-redis -p 6380:6379 redis:7-alpine
```

```powershell
$env:ConnectionStrings__Redis = "localhost:6380"
$env:Cache__Provider = "Redis"
dotnet run --project SantanderHackerNews.Api
```

### Hacker News is unavailable

The HTTP client uses timeouts, exponential jittered retries (honoring
upstream `Retry-After` where supplied), and a circuit breaker. When possible,
the service serves a previously cached response. Check the API logs, verify
internet access, and retry after the upstream service recovers. Concurrent
refreshes are coalesced, outbound item requests are bounded, and the
configurable outbound request-rate limiter prevents upstream bursts.

## Shutdown and cleanup

Stop the API with `Ctrl+C`. If Redis was started, stop it when it is no longer needed:

```bash
docker stop santander-redis
```

To stop and remove the container and its temporary data:

```bash
docker rm -f santander-redis
```

The `redis:7-alpine` container is started without a volume, so removing it
also removes data stored in that container.
