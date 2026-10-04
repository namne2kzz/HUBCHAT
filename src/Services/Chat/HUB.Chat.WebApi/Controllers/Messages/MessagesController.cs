using HUB.Chat.Application.Common.Models;
using HUB.Chat.Application.Messages.Commands.AddReaction;
using HUB.Chat.Application.Messages.Commands.PostMessage;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Application.Messages.Queries.ListMessages;
using HUB.Chat.WebApi.Controllers.Messages.Requests;
using HUB.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HUB.Chat.WebApi.Controllers.Messages;

/// <summary>Message + reaction endpoints.</summary>
/// <param name="mediator">MediatR sender.</param>
/// <param name="currentUser">The authenticated user.</param>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class MessagesController(ISender mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Posts a message to a channel.</summary>
    /// <param name="channelId">Target channel.</param>
    /// <param name="request">Message content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the created message. A retry carrying the same <c>ClientMessageId</c> gets the same 201 and
    /// the original message — the response a lost first reply would have carried.
    /// </returns>
    [HttpPost("channels/{channelId:guid}/messages")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Post(Guid channelId, [FromBody] PostMessageRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new PostMessageCommand(
            channelId, request.Body, request.Format, request.ParentId,
            request.MentionedUserIds ?? [], currentUser.Id, request.ClientMessageId), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>Lists a channel's top-level messages (newest first, keyset pagination).</summary>
    /// <param name="channelId">Channel id.</param>
    /// <param name="cursor">Opaque cursor for the next older page.</param>
    /// <param name="limit">Page size (1..100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with a cursor page of messages.</returns>
    [HttpGet("channels/{channelId:guid}/messages")]
    [ProducesResponseType<CursorPage<MessageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid channelId, [FromQuery] string? cursor, [FromQuery] int limit, CancellationToken ct)
    {
        var result = await mediator.Send(new ListMessagesQuery(channelId, currentUser.Id, cursor, limit == 0 ? 50 : limit), ct);
        return Ok(result);
    }

    /// <summary>Adds an emoji reaction to a message.</summary>
    /// <param name="messageId">Message id.</param>
    /// <param name="request">Reaction emoji.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("messages/{messageId:guid}/reactions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> React(Guid messageId, [FromBody] AddReactionRequest request, CancellationToken ct)
    {
        await mediator.Send(new AddReactionCommand(messageId, request.Emoji, currentUser.Id), ct);
        return NoContent();
    }
}
