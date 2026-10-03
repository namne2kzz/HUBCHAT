using MediatR;

namespace HUB.Chat.Application.Channels.Queries.CanJoinChannel;

/// <summary>
/// Answers whether a user may subscribe to a channel's realtime stream — public channels are open,
/// private ones require membership.
/// </summary>
/// <remarks>
/// Returns a bool rather than throwing, because the caller is the realtime hub deciding whether to add
/// a connection to a SignalR group, not an API deciding a status code.
/// </remarks>
/// <param name="ChannelId">Channel the user wants to join.</param>
/// <param name="UserId">The user asking to join.</param>
public sealed record CanJoinChannelQuery(Guid ChannelId, Guid UserId) : IRequest<bool>;
