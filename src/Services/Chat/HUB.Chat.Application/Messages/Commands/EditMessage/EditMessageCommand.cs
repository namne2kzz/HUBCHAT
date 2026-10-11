using MediatR;
using HUB.Chat.Application.Messages.DTOs;

namespace HUB.Chat.Application.Messages.Commands.EditMessage;

/// <summary>Edits the body of the caller's own message.</summary>
/// <param name="MessageId">Message to edit.</param>
/// <param name="Body">New body.</param>
/// <param name="ActingUserId">The user editing — only the author may edit.</param>
public sealed record EditMessageCommand(Guid MessageId, string Body, Guid ActingUserId) : IRequest<MessageDto>;
