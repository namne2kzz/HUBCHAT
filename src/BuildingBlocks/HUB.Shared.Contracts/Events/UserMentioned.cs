namespace HUB.Shared.Contracts.Events;

/// <summary>Published when a user is @mentioned in a message; consumed by notification-service.</summary>
/// <param name="MentionedUserId">The mentioned user.</param>
/// <param name="ChannelId">Channel where the mention occurred.</param>
/// <param name="MessageId">Message containing the mention.</param>
/// <param name="ByUserId">User who wrote the mention.</param>
public sealed record UserMentioned(
    Guid MentionedUserId,
    Guid ChannelId,
    Guid MessageId,
    Guid ByUserId) : IntegrationEvent;
