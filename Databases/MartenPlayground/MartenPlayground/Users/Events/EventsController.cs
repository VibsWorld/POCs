using System.ComponentModel.DataAnnotations;
using Marten;
using Marten.Patching;
using MartenPlayground.Users.Controller;
using MartenPlayground.Users.Domain;
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
    #endregion
}
