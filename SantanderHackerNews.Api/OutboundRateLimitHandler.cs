using Microsoft.Extensions.Options;

namespace SantanderHackerNews.Api;

/// Spaces outbound requests to avoid overwhelming the upstream API.
public sealed class OutboundRateLimitHandler(IOptions<HackerNewsOptions> options) : DelegatingHandler
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequest = DateTimeOffset.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var delay = nextRequest - now;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            nextRequest = DateTimeOffset.UtcNow.AddSeconds(1d / options.Value.OutboundRequestsPerSecond);
        }
        finally { gate.Release(); }
        return await base.SendAsync(request, cancellationToken);
    }
}
