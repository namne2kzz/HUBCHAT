namespace HUB.Shared.Contracts.Events;

/// <summary>Published by chat-service after a message is persisted; consumed by realtime + notification services.</summary>
/// <param name="MessageId">The new message id.</param>
/// <param name="ChannelId">Channel the message belongs to.</param>
/// <param name="AuthorId">User who sent the message.</param>
/// <param name="Preview">Short plain-text preview (for notifications).</param>
/// <param name="MentionedUserIds">Users explicitly @mentioned.</param>
public sealed record MessageSent(
    Guid MessageId,
    Guid ChannelId,
    Guid AuthorId,
    string Preview,
    IReadOnlyCollection<Guid> MentionedUserIds) : IntegrationEvent;
