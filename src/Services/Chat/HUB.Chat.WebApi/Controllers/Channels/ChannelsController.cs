using HUB.Chat.Application.Channels.Commands.AddChannelMember;
using HUB.Chat.Application.Channels.Commands.RemoveChannelMember;
using HUB.Chat.Application.Channels.Commands.CreateChannel;
using HUB.Chat.Application.Channels.Commands.JoinChannel;
using HUB.Chat.Application.Channels.Commands.LeaveChannel;
using HUB.Chat.Application.Channels.Commands.MarkRead;
using HUB.Chat.Application.Channels.Commands.OpenDirectMessage;
using HUB.Chat.Application.Channels.Commands.OpenLinkedThread;
using HUB.Chat.Application.Channels.Commands.ChangeChannelVisibility;
using HUB.Chat.Application.Channels.Commands.TransferOwnership;
using HUB.Chat.Application.Channels.Commands.UpdateChannel;
using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Channels.Queries.GetChannel;
using HUB.Chat.Application.Channels.Queries.ListChannelMembers;
using HUB.Chat.Application.Channels.Queries.ListChannels;
using HUB.Chat.WebApi.Controllers.Channels.Requests;
using HUB.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HUB.Chat.WebApi.Controllers.Channels;

/// <summary>Channel management endpoints.</summary>
/// <param name="mediator">MediatR sender.</param>
/// <param name="currentUser">The authenticated user.</param>
[ApiController]
[Route("api/v1/channels")]
[Authorize]
public sealed class ChannelsController(ISender mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Creates a channel; the caller becomes owner.</summary>
    /// <param name="request">Channel details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the created channel.</returns>
    [HttpPost]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateChannelRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(
            new CreateChannelCommand(request.WorkspaceId, request.Name, request.Type, request.Topic ?? string.Empty, currentUser.Id), ct);
        return CreatedAtAction(nameof(List), new { workspaceId = dto.WorkspaceId }, dto);
    }

    /// <summary>Lists channels in a workspace visible to the caller.</summary>
    /// <param name="workspaceId">Workspace id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the channels.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChannelDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid workspaceId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListChannelsQuery(workspaceId, currentUser.Id), ct);
        return Ok(result);
    }

    /// <summary>Gets a single channel by id (public, or one the caller belongs to).</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the channel, 403 if not a member, 404 if not found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetChannelQuery(id, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Adds the caller to a channel.</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        await mediator.Send(new JoinChannelCommand(id, currentUser.Id), ct);
        return NoContent();
    }

    /// <summary>Marks the channel read for the caller.</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="request">Read timestamp.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, [FromBody] MarkReadRequest request, CancellationToken ct)
    {
        await mediator.Send(new MarkReadCommand(id, currentUser.Id, request.ReadAt), ct);
        return NoContent();
    }

    /// <summary>Removes the caller from a channel.</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{id:guid}/members/me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await mediator.Send(new LeaveChannelCommand(id, currentUser.Id), ct);
        return NoContent();
    }

    /// <summary>Adds another user to the channel (admin/owner only). Idempotent — safe to call if already a member.</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="userId">User to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the membership DTO.</returns>
    [HttpPut("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType<ChannelMemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMember(Guid id, Guid userId, CancellationToken ct)
    {
        var dto = await mediator.Send(new AddChannelMemberCommand(id, userId, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Removes a member from the channel (admin/owner or ManageChannels permission).</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="userId">User to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 403 if caller lacks permission; 400 if removing the last owner.</returns>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await mediator.Send(new RemoveChannelMemberCommand(id, userId), ct);
        return NoContent();
    }

    /// <summary>Lists the members of a channel.</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the members.</returns>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType<IReadOnlyList<ChannelMemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMembers(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ListChannelMembersQuery(id, currentUser.Id), ct);
        return Ok(result);
    }

    /// <summary>Updates a channel's name and/or topic (admin/owner only).</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="request">Fields to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the updated channel.</returns>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new UpdateChannelCommand(id, request.Name, request.Topic, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Switches a channel between Public and Private (owner only).</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="request">Desired visibility.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the updated channel.</returns>
    [HttpPatch("{id:guid}/visibility")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeVisibility(Guid id, [FromBody] ChangeVisibilityRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new ChangeChannelVisibilityCommand(id, request.IsPrivate, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Transfers channel ownership to another member (owner only). The caller becomes an Admin.</summary>
    /// <param name="id">Channel id.</param>
    /// <param name="request">The new owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the updated channel.</returns>
    [HttpPut("{id:guid}/owner")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new TransferOwnershipCommand(id, request.NewOwnerUserId, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Opens (find-or-create) the canonical direct-message channel with another user.</summary>
    /// <param name="targetUserId">The other participant.</param>
    /// <param name="workspaceId">Workspace used if a new DM must be created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the DM channel (existing or newly created).</returns>
    [HttpPost("dm/{targetUserId:guid}")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenDirectMessage(Guid targetUserId, [FromQuery] Guid workspaceId, CancellationToken ct)
    {
        var dto = await mediator.Send(new OpenDirectMessageCommand(workspaceId, targetUserId, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Opens (find-or-create) a discussion thread linked to a DASHBOARD work item / resource.</summary>
    /// <param name="request">Link details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the linked channel (existing or newly created).</returns>
    [HttpPost("linked")]
    [ProducesResponseType<ChannelDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenLinkedThread([FromBody] OpenLinkedThreadRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new OpenLinkedThreadCommand(
            request.WorkspaceId, request.LinkType, request.ExternalId, request.ExternalKey,
            request.Title ?? string.Empty, request.Url ?? string.Empty, currentUser.Id), ct);
        return Ok(dto);
    }
}
