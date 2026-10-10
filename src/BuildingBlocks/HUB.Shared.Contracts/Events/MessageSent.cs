namespace HUB.Shared.Contracts.Events;

/// <summary>Published by chat-service after a message is persisted; consumed by realtime + notification services.</summary>
/// <remarks>
/// Carries the whole message as it exists at post time (no reactions/attachments yet), so realtime can push
/// a complete message instead of a 140-char stub. The trailing fields are optional and appended last: events
/// published before they existed deserialize with nulls, and the client falls back to <see cref="Preview"/>.
/// </remarks>
/// <param name="MessageId">The new message id.</param>
/// <param name="ChannelId">Channel the message belongs to.</param>
/// <param name="AuthorId">User who sent the message.</param>
/// <param name="Preview">Short plain-text preview (for notifications, sidebar).</param>
/// <param name="MentionedUserIds">Users explicitly @mentioned.</param>
/// <param name="Body">Full message body (≤ 8000 chars).</param>
/// <param name="Format">Body format — numeric value of chat's <c>MessageFormat</c> (0 = Plain, 1 = Markdown).</param>
/// <param name="ParentId">Thread parent id for a reply; null for a top-level message.</param>
/// <param name="CreatedAt">UTC creation time of the message (the timeline sort key).</param>
public sealed record MessageSent(
    Guid MessageId,
    Guid ChannelId,
    Guid AuthorId,
    string Preview,
    IReadOnlyCollection<Guid> MentionedUserIds,
    string? Body = null,
    int? Format = null,
    Guid? ParentId = null,
    DateTime? CreatedAt = null) : IntegrationEvent;
