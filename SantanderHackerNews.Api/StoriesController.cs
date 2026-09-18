using Microsoft.AspNetCore.Mvc;

namespace SantanderHackerNews.Api;

[ApiController, Route("api/stories")]
public sealed class StoriesController(BestStoriesService service) : ControllerBase
{
    [HttpGet("best")]
    [ProducesResponseType(typeof(IReadOnlyList<StoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> Best(
        [FromQuery] int n = 10,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        if (n is < 1 or > 100)
            return BadRequest("n must be between 1 and 100.");

        var stories = await service.GetAsync(n, refresh, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(stories);
    }
}
