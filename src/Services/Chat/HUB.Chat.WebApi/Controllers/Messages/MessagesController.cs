using HUB.Chat.Application.Common.Models;
using HUB.Chat.Application.Messages.Commands.AddReaction;
using HUB.Chat.Application.Messages.Commands.PostMessage;
using HUB.Chat.Application.Messages.Commands.DeleteMessage;
using HUB.Chat.Application.Messages.Commands.EditMessage;
using HUB.Chat.Application.Messages.Commands.ForwardMessage;
using HUB.Chat.Application.Messages.Commands.RemoveReaction;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Application.Messages.Queries.ListChannelAttachments;
using HUB.Chat.Application.Messages.Queries.ListMessages;
using HUB.Chat.Application.Messages.Queries.ListMessagesAfter;
using HUB.Chat.Application.Messages.Commands.PinMessage;
using HUB.Chat.Application.Messages.Queries.ListPinnedMessages;
using HUB.Chat.Domain.Enums;
using HUB.Chat.Application.Messages.Queries.ListThreadReplies;
using HUB.Chat.Application.Messages.Queries.SearchMessages;
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
            request.MentionedUserIds ?? [], currentUser.Id, request.ClientMessageId, request.ReplyToId,
            request.Attachments), ct);
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

    /// <summary>Lists top-level messages newer than a given message (oldest first) — reconnect catch-up.</summary>
    /// <param name="channelId">Channel id.</param>
    /// <param name="messageId">The newest message the client already has.</param>
    /// <param name="limit">Page size (1..100, default 100). Fewer items than this means caught up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the newer messages; 404 when the anchor is not in this channel (client should reload).</returns>
    [HttpGet("channels/{channelId:guid}/messages/after/{messageId:guid}")]
    [ProducesResponseType<IReadOnlyList<MessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAfter(Guid channelId, Guid messageId, [FromQuery] int limit, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListMessagesAfterQuery(channelId, currentUser.Id, messageId, limit == 0 ? 100 : limit), ct);
        return Ok(result);
    }

    /// <summary>Pins a message in its channel (any member).</summary>
    /// <param name="messageId">Message to pin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 403 if not a member.</returns>
    [HttpPost("messages/{messageId:guid}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Pin(Guid messageId, CancellationToken ct)
    {
        await mediator.Send(new PinMessageCommand(messageId, Pin: true, currentUser.Id), ct);
        return NoContent();
    }

    /// <summary>Unpins a message (any member).</summary>
    /// <param name="messageId">Message to unpin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("messages/{messageId:guid}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unpin(Guid messageId, CancellationToken ct)
    {
        await mediator.Send(new PinMessageCommand(messageId, Pin: false, currentUser.Id), ct);
        return NoContent();
    }

    /// <summary>Lists a channel's pinned messages (most recently pinned first).</summary>
    /// <param name="channelId">Channel id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the pinned messages.</returns>
    [HttpGet("channels/{channelId:guid}/messages/pinned")]
    [ProducesResponseType<IReadOnlyList<MessageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPinned(Guid channelId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListPinnedMessagesQuery(channelId, currentUser.Id), ct);
        return Ok(result);
    }

    /// <summary>Lists attachments shared in a channel (Files/Media panel, CF-5), newest first.</summary>
    /// <param name="channelId">Channel id.</param>
    /// <param name="kind">Optional filter: 0 = File, 1 = Image, 2 = Video; omit for all.</param>
    /// <param name="limit">Max rows (1..100, default 60).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the channel's attachments.</returns>
    [HttpGet("channels/{channelId:guid}/messages/attachments")]
    [ProducesResponseType<IReadOnlyList<ChannelAttachmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAttachments(
        Guid channelId, [FromQuery] AttachmentKind? kind, [FromQuery] int limit, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListChannelAttachmentsQuery(channelId, currentUser.Id, kind, limit == 0 ? 60 : limit), ct);
        return Ok(result);
    }

    /// <summary>Searches a channel's full message history by text.</summary>
    /// <param name="channelId">Channel to search.</param>
    /// <param name="q">Search term.</param>
    /// <param name="limit">Max results (1..50, default 30).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with matching messages (newest first).</returns>
    [HttpGet("channels/{channelId:guid}/messages/search")]
    [ProducesResponseType<IReadOnlyList<MessageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(Guid channelId, [FromQuery] string q, [FromQuery] int limit, CancellationToken ct)
    {
        var result = await mediator.Send(new SearchMessagesQuery(channelId, q ?? string.Empty, currentUser.Id, limit == 0 ? 30 : limit), ct);
        return Ok(result);
    }

    /// <summary>Forwards a message into another channel.</summary>
    /// <param name="messageId">Source message to forward.</param>
    /// <param name="request">Target channel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the forwarded message; 403 if not allowed; 404 if source/target missing.</returns>
    [HttpPost("messages/{messageId:guid}/forward")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Forward(Guid messageId, [FromBody] ForwardMessageRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new ForwardMessageCommand(messageId, request.TargetChannelId, currentUser.Id), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>Lists the replies of a thread (a top-level message), oldest first.</summary>
    /// <param name="channelId">Channel id.</param>
    /// <param name="messageId">The top-level message whose thread to open.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the thread's replies; 404 if the parent is not in this channel.</returns>
    [HttpGet("channels/{channelId:guid}/messages/{messageId:guid}/replies")]
    [ProducesResponseType<IReadOnlyList<MessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListReplies(Guid channelId, Guid messageId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListThreadRepliesQuery(channelId, messageId, currentUser.Id), ct);
        return Ok(result);
    }

    /// <summary>Edits the caller's own message (author only, within the edit window).</summary>
    /// <param name="messageId">Message id.</param>
    /// <param name="request">New body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the edited message; 403 if not the author; 409 if too old or deleted.</returns>
    [HttpPatch("messages/{messageId:guid}")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Edit(Guid messageId, [FromBody] EditMessageRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new EditMessageCommand(messageId, request.Body, currentUser.Id), ct);
        return Ok(dto);
    }

    /// <summary>Deletes a message — author (within the window) or a channel moderator. Idempotent.</summary>
    /// <param name="messageId">Message id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 403 if not allowed.</returns>
    [HttpDelete("messages/{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken ct)
    {
        await mediator.Send(new DeleteMessageCommand(messageId, currentUser.Id), ct);
        return NoContent();
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

    /// <summary>Removes the caller's own emoji reaction from a message (idempotent).</summary>
    /// <param name="messageId">Message id.</param>
    /// <param name="emoji">Emoji to remove — a query parameter, since emoji do not travel well as path segments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 whether or not the reaction existed.</returns>
    [HttpDelete("messages/{messageId:guid}/reactions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unreact(Guid messageId, [FromQuery] string emoji, CancellationToken ct)
    {
        await mediator.Send(new RemoveReactionCommand(messageId, emoji, currentUser.Id), ct);
        return NoContent();
    }
}
