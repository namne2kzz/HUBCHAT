using HUB.Chat.Application.Messages.DTOs;
using MediatR;

namespace HUB.Chat.Application.Messages.Queries.SearchMessages;

/// <summary>Searches a channel's messages by body text (case-insensitive substring).</summary>
/// <param name="ChannelId">Channel to search.</param>
/// <param name="Term">Search text (trimmed; blank returns nothing).</param>
/// <param name="ActingUserId">Requesting user (must be able to read the channel).</param>
/// <param name="Limit">Max results (clamped 1..50).</param>
public sealed record SearchMessagesQuery(Guid ChannelId, string Term, Guid ActingUserId, int Limit = 30)
    : IRequest<IReadOnlyList<MessageDto>>;
