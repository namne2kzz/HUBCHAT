using HUB.Chat.Application.Messages.DTOs;
using MediatR;

namespace HUB.Chat.Application.Messages.Queries.ListPinnedMessages;

/// <summary>Lists a channel's pinned messages, most recently pinned first.</summary>
/// <param name="ChannelId">Channel to read.</param>
/// <param name="ActingUserId">Requesting user (must be able to read the channel).</param>
public sealed record ListPinnedMessagesQuery(Guid ChannelId, Guid ActingUserId) : IRequest<IReadOnlyList<MessageDto>>;
