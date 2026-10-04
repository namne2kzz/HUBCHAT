using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Messages.DTOs;

/// <summary>A single reaction summary.</summary>
/// <param name="Emoji">Emoji shortcode.</param>
/// <param name="UserId">User who reacted.</param>
public sealed record ReactionDto(string Emoji, Guid UserId);
