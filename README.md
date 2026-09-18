# Santander Hacker News API

Production-oriented ASP.NET Core 8 API exposing Hacker News best stories. The
API uses Redis for distributed caching and the Hacker News Firebase API as its
upstream data source.

## Prerequisites

Install the following tools before starting:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git, to clone the repository

Verify the .NET SDK and Docker Desktop installation:

```bash
dotnet --version
docker --version
docker info
```

The SDK version must be 8.x. `docker info` must complete successfully; if it
cannot connect to the Docker daemon, start Docker Desktop and try again.

## Start Redis

Start a Redis 7 container before running the API:

```bash
docker run -d --name santander-redis -p 6379:6379 redis:7-alpine
```

This publishes Redis on `localhost:6379`, which is the connection string used
by the API by default. Verify that the container is running and accepting
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
$env:ConnectionStrings__Redis = "localhost:6379"
$env:HackerNews__StoryCount = "20"
$env:HackerNews__CacheSeconds = "60"
$env:HackerNews__StaleCacheSeconds = "300"
$env:HackerNews__TimeoutSeconds = "10"
$env:HackerNews__MaxConcurrentItemRequests = "8"
$env:HackerNews__OutboundRequestsPerSecond = "20"
$env:HackerNews__RefreshIntervalSeconds = "30"
dotnet run --project SantanderHackerNews.Api
```

The available settings are:

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Redis` | `localhost:6379` | Redis connection string |
| `HackerNews__BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Hacker News API base URL |
| `HackerNews__StoryCount` | `20` | Number of stories refreshed by the background service |
| `HackerNews__CacheSeconds` | `60` | Fresh cache lifetime |
| `HackerNews__StaleCacheSeconds` | `300` | Stale-cache configuration value |
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

## Swagger

With the API running, open
[http://localhost:5233/swagger](http://localhost:5233/swagger) to view and
execute the OpenAPI operations interactively.

## Tests

Run the full test suite from the repository root:

```bash
dotnet test
```

Tests use an in-memory distributed cache and fake HTTP handlers, so they do not
require Redis, Docker, or internet access.

## Troubleshooting

### Docker Desktop is unavailable

Start Docker Desktop and wait until `docker info` succeeds. If Docker cannot
be used on the machine, install or run a Redis-compatible server locally and
set `ConnectionStrings__Redis` to its host and port before starting the API.
The API currently expects Redis at startup/runtime; without a reachable Redis
instance, API requests that access the cache will fail. The test suite remains
available without Redis because it replaces Redis with an in-memory cache.

### Redis is not reachable

Check the container and its logs:

```bash
docker ps -a --filter "name=santander-redis"
docker logs santander-redis
docker start santander-redis
docker exec santander-redis redis-cli ping
```

Confirm that the API's `ConnectionStrings__Redis` value matches the published
host and port. If port 6379 is already in use, publish another host port and
use the matching value, for example:

```bash
docker run -d --name santander-redis -p 6380:6379 redis:7-alpine
```

```powershell
$env:ConnectionStrings__Redis = "localhost:6380"
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

Stop the API with `Ctrl+C`. Stop Redis when it is no longer needed:

```bash
docker stop santander-redis
```

To stop and remove the container and its temporary data:

```bash
docker rm -f santander-redis
```

The `redis:7-alpine` container is started without a volume, so removing it
also removes data stored in that container.
