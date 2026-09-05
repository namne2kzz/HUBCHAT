using MediatR;

namespace HUB.Chat.Application.Messages.Commands.AddReaction;

/// <summary>Adds an emoji reaction to a message on behalf of the acting user.</summary>
/// <param name="MessageId">Target message.</param>
/// <param name="Emoji">Emoji shortcode.</param>
/// <param name="ActingUserId">Reacting user.</param>
public sealed record AddReactionCommand(Guid MessageId, string Emoji, Guid ActingUserId) : IRequest;
