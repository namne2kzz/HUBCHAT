using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Messages.Requests;

/// <summary>Body for reacting to a message.</summary>
/// <param name="Emoji">Emoji shortcode.</param>
public sealed record AddReactionRequest(string Emoji);
