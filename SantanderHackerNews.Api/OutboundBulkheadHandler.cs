namespace SantanderHackerNews.Api;
using Microsoft.Extensions.Options;

/// Limits simultaneous calls to Hacker News across all requests.
public sealed class OutboundBulkheadHandler(IOptions<HackerNewsOptions> options) : DelegatingHandler
{
    private readonly SemaphoreSlim semaphore = new(options.Value.MaxConcurrentItemRequests);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try { return await base.SendAsync(request, cancellationToken); }
        finally { semaphore.Release(); }
    }
}
