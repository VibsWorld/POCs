using Marten;
using Microsoft.AspNetCore.Mvc;

namespace MartenPlayground.Users.Events;

[ApiController]
[Route("[controller]")]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> logger;
    private readonly IDocumentSession session;
    private readonly IQuerySession sessionQuery;

    public EventsController(
        ILogger<EventsController> logger,
        IDocumentSession session,
        IQuerySession sessionQuery
    )
    {
        this.logger = logger;
        this.session = session;
        this.sessionQuery = sessionQuery;
    }

    #region Events
    [HttpGet("FetchStream/{Id:guid}")]
    public async Task<IActionResult> GetEventsStream(Guid Id)
    {
        var stream = await session.Events.FetchStreamAsync(Id);
        Queue<object> que = new();
        foreach (var evt in stream)
        {
            que.Enqueue(
                new
                {
                    evt.EventTypeName,
                    evt.DotNetTypeName,
                    evt.Data,
                }
            );
        }
        return Ok(que);
    }

    [HttpGet("GetUserDashboardViewProjection/{Id:guid}")]
    public async Task<IActionResult> GetUserDashboardStatus(Guid Id) =>
        Ok(await session.LoadAsync<UserDashboardStats>(Id));
    #endregion
}
