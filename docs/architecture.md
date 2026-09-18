# Architecture and design

## Boundaries

The solution is a small service-oriented ASP.NET Core API with explicit
boundaries:

- `StoriesController` owns HTTP concerns and validates the public `n` contract.
- `BestStoriesService` owns orchestration, concurrency limits, ranking, and
  cache-aside behavior.
- `HackerNewsClient` is an outbound adapter behind `IHackerNewsClient`, making
  upstream I/O replaceable in tests.
- `IDistributedCache` supports local development with memory and horizontally
  scalable deployments with Redis.

The API does not need a relational database: Hacker News is the system of
record and the cache is a read-through performance store. If persisted
features are added, the service boundary should hide the chosen SQL/NoSQL
repository rather than exposing provider types from controllers.

## Reliability and operations

Outbound calls use bounded concurrency, rate limiting, timeouts, retries, and a
circuit breaker. Refreshes are coalesced with a semaphore so concurrent
requests do not stampede the upstream API. A failed refresh can return an
existing cached response, while cancellation is allowed to propagate.

`/health/live` is a process liveness probe and `/health/ready` is the readiness
probe used by orchestrators. `X-Request-Id` is accepted or generated and
returned on every response so logs can be correlated across service calls.
Structured logs record refresh and upstream failures without logging payloads.

## Delivery and testing

The GitHub Actions workflow restores dependencies, builds in Release, runs the
unit/integration tests with coverage collection, and verifies the
Docker image builds. Tests use fake HTTP and Hacker News clients, so normal CI
does not depend on the internet or Redis. The seams also support contract tests
against a controlled upstream fixture.

The application is stateless apart from its configured distributed cache and
can be scaled horizontally. Messaging is not required for the synchronous
read path; a queue/event would be appropriate if refresh work later becomes
long-running or needs durable retries.
