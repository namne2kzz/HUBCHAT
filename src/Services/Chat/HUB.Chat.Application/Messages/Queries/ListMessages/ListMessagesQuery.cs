using HUB.Chat.Application.Common.Models;
using HUB.Chat.Application.Messages.DTOs;
using MediatR;

namespace HUB.Chat.Application.Messages.Queries.ListMessages;

/// <summary>Lists top-level messages of a channel, newest first, using keyset pagination.</summary>
/// <param name="ChannelId">Channel to read.</param>
/// <param name="ActingUserId">Requesting user (must be a member of private channels).</param>
/// <param name="Cursor">Opaque cursor for the next (older) page; null for the first page.</param>
/// <param name="Limit">Page size (clamped 1..100).</param>
public sealed record ListMessagesQuery(
    Guid ChannelId,
    Guid ActingUserId,
    string? Cursor,
    int Limit) : IRequest<CursorPage<MessageDto>>;
