using HUB.Notification.Application.Notifications.Commands.MarkAllRead;
using HUB.Notification.Application.Notifications.Commands.MarkRead;
using HUB.Notification.Application.Notifications.DTOs;
using HUB.Notification.Application.Notifications.Queries.ListNotifications;
using HUB.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HUB.Notification.WebApi.Controllers.Notifications;

/// <summary>In-app notification endpoints for the current user.</summary>
/// <param name="mediator">MediatR sender.</param>
/// <param name="currentUser">The authenticated user.</param>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(ISender mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Lists the caller's notifications (newest first).</summary>
    /// <param name="unreadOnly">Filter to unread only.</param>
    /// <param name="limit">Page size (1..100, default 50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the notifications.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly, [FromQuery] int limit, CancellationToken ct)
    {
        var result = await mediator.Send(new ListNotificationsQuery(currentUser.Id, unreadOnly, limit == 0 ? 50 : limit), ct);
        return Ok(result);
    }

    /// <summary>Marks a single notification read.</summary>
    /// <param name="id">Notification id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await mediator.Send(new MarkNotificationReadCommand(id, currentUser.Id), ct);
        return NoContent();
    }

    /// <summary>Marks all of the caller's notifications read.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await mediator.Send(new MarkAllReadCommand(currentUser.Id), ct);
        return NoContent();
    }
}
