using HUB.Chat.Application.Messages.DTOs;
using MediatR;

namespace HUB.Chat.Application.Messages.Commands.ForwardMessage;

/// <summary>Forwards an existing message into another channel.</summary>
/// <param name="SourceMessageId">The message being forwarded.</param>
/// <param name="TargetChannelId">Channel to forward it into.</param>
/// <param name="ActingUserId">User doing the forward — must read the source and be a member of the target.</param>
public sealed record ForwardMessageCommand(Guid SourceMessageId, Guid TargetChannelId, Guid ActingUserId)
    : IRequest<MessageDto>;
