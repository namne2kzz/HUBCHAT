using HUB.Chat.Application.Messages.DTOs;
using MediatR;

namespace HUB.Chat.Application.Messages.Queries.ListThreadReplies;

/// <summary>Lists the replies of a thread (a top-level message), oldest first.</summary>
/// <param name="ChannelId">Channel the thread lives in (for the read-access check).</param>
/// <param name="ParentId">The top-level message whose replies to list.</param>
/// <param name="ActingUserId">Requesting user (must be able to read the channel).</param>
public sealed record ListThreadRepliesQuery(Guid ChannelId, Guid ParentId, Guid ActingUserId)
    : IRequest<IReadOnlyList<MessageDto>>;
