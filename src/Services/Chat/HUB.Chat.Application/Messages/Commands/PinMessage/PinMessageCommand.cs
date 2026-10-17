using MediatR;

namespace HUB.Chat.Application.Messages.Commands.PinMessage;

/// <summary>Pins or unpins a message in its channel.</summary>
/// <param name="MessageId">Target message.</param>
/// <param name="Pin">True to pin, false to unpin.</param>
/// <param name="ActingUserId">User performing it (must be a channel member).</param>
public sealed record PinMessageCommand(Guid MessageId, bool Pin, Guid ActingUserId) : IRequest;
