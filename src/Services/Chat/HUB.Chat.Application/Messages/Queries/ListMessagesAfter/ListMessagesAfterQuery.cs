using HUB.Chat.Application.Messages.DTOs;
using MediatR;

namespace HUB.Chat.Application.Messages.Queries.ListMessagesAfter;

/// <summary>
/// Lists a channel's top-level messages <b>newer</b> than an anchor message, oldest first — for a client
/// catching up on what it missed while disconnected.
/// </summary>
/// <param name="ChannelId">Channel to read.</param>
/// <param name="ActingUserId">Requesting user (must be a member of private channels).</param>
/// <param name="AfterMessageId">The newest message the client already has.</param>
/// <param name="Limit">Page size (clamped 1..100). Fewer items than this means the client is caught up.</param>
public sealed record ListMessagesAfterQuery(
    Guid ChannelId,
    Guid ActingUserId,
    Guid AfterMessageId,
    int Limit) : IRequest<IReadOnlyList<MessageDto>>;
