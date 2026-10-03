using HUB.Chat.Application.Channels.Commands.AddChannelMember;
using HUB.Chat.Application.Channels.Commands.ArchiveChannel;
using HUB.Chat.Application.Channels.Commands.FindOrCreateSprintChannel;
using HUB.Chat.Application.Channels.Commands.RemoveChannelMember;
using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Channels.Queries.CanJoinChannel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HUB.Chat.WebApi.Controllers.Internal;

/// <summary>
/// Machine-to-machine endpoints consumed by DASHBOARD backend over the internal Docker network.
/// Protected by <see cref="Middleware.InternalApiKeyMiddleware"/> (X-Internal-Token header) — no JWT.
/// </summary>
/// <param name="mediator">MediatR sender.</param>
[ApiController]
[Route("internal/channels")]
[AllowAnonymous]
public sealed class InternalChannelsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Finds or creates a Private channel linked to a DASHBOARD sprint.
    /// Idempotent — calling again for the same sprint returns the existing channel.
    /// </summary>
    /// <param name="request">Sprint info needed to create/find the channel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 on create, 200 if channel already existed.</returns>
    [HttpPost("sprint")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> FindOrCreateSprintChannel(
        [FromBody] FindOrCreateSprintChannelRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new FindOrCreateSprintChannelCommand(
            request.WorkspaceId, request.SprintId, request.SprintName, request.CreatorUserId), ct);
        return Ok(dto);
    }

    /// <summary>
    /// Reports whether a user may subscribe to a channel's realtime stream.
    /// </summary>
    /// <remarks>
    /// Consumed by the realtime service before adding a connection to a SignalR group. Returns 200 with
    /// a boolean rather than 403, because the caller is deciding, not being denied.
    /// </remarks>
    /// <param name="channelId">Channel the user wants to join.</param>
    /// <param name="userId">The user asking to join.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <c>{ "allowed": true|false }</c>.</returns>
    [HttpGet("{channelId:guid}/can-join/{userId:guid}")]
    [ProducesResponseType<CanJoinChannelResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CanJoin(Guid channelId, Guid userId, CancellationToken ct)
    {
        var allowed = await mediator.Send(new CanJoinChannelQuery(channelId, userId), ct);
        return Ok(new CanJoinChannelResponse(allowed));
    }

    /// <summary>
    /// Force-adds a user to a channel (sprint member sync). Idempotent.
    /// </summary>
    /// <param name="channelId">Target channel.</param>
    /// <param name="userId">User to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{channelId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddMember(Guid channelId, Guid userId, CancellationToken ct)
    {
        await mediator.Send(new AddChannelMemberCommand(channelId, userId), ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a user from a channel (sprint capacity member removed). Silently succeeds if not a member.
    /// </summary>
    /// <param name="channelId">Target channel.</param>
    /// <param name="userId">User to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{channelId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid channelId, Guid userId, CancellationToken ct)
    {
        await mediator.Send(new RemoveChannelMemberCommand(channelId, userId), ct);
        return NoContent();
    }

    /// <summary>
    /// Archives a channel. Called when the linked sprint is closed. Idempotent.
    /// </summary>
    /// <param name="channelId">Channel to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{channelId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid channelId, CancellationToken ct)
    {
        await mediator.Send(new ArchiveChannelCommand(channelId), ct);
        return NoContent();
    }
}

// ── Request records ───────────────────────────────────────────────────────────

/// <summary>Whether a user may subscribe to a channel's realtime stream.</summary>
/// <param name="Allowed">True when the channel is public or the user is a member.</param>
public sealed record CanJoinChannelResponse(bool Allowed);

/// <summary>Payload for the find-or-create sprint channel endpoint.</summary>
public sealed record FindOrCreateSprintChannelRequest(
    Guid   WorkspaceId,
    Guid   SprintId,
    string SprintName,
    Guid   CreatorUserId);
