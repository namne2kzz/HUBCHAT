using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Enums;
using MediatR;

namespace HUB.Chat.Application.Messages.Commands.PostMessage;

/// <summary>Posts a message (top-level or thread reply) to a channel.</summary>
/// <param name="ChannelId">Target channel.</param>
/// <param name="Body">Message body.</param>
/// <param name="Format">Body format.</param>
/// <param name="ParentId">Parent message id for a thread reply; null for top-level.</param>
/// <param name="MentionedUserIds">User ids @mentioned.</param>
/// <param name="ActingUserId">Author (from the JWT).</param>
/// <param name="ClientMessageId">Client idempotency key; a retry with the same key returns the original message. Null = no dedupe.</param>
public sealed record PostMessageCommand(
    Guid ChannelId,
    string Body,
    MessageFormat Format,
    Guid? ParentId,
    IReadOnlyList<Guid> MentionedUserIds,
    Guid ActingUserId,
    Guid? ClientMessageId = null) : IRequest<MessageDto>;
