using HUB.Notification.Domain.Common;
using HUB.Notification.Domain.Enums;

namespace HUB.Notification.Domain.Entities;

/// <summary>An in-app notification delivered to a user. (Named UserNotification to avoid clashing with the HUB.Notification namespace.)</summary>
public sealed class UserNotification : Entity
{
    private UserNotification() { } // EF

    private UserNotification(Guid userId, NotificationType type, Guid? sourceId, Guid? channelId, Guid? byUserId, string preview)
    {
        UserId    = userId;
        Type      = type;
        SourceId  = sourceId;
        ChannelId = channelId;
        ByUserId  = byUserId;
        Preview   = preview;
    }

    /// <summary>Recipient user id.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Notification kind.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>Source entity id (e.g. message id).</summary>
    public Guid? SourceId { get; private set; }

    /// <summary>Channel the notification relates to.</summary>
    public Guid? ChannelId { get; private set; }

    /// <summary>User who triggered the notification.</summary>
    public Guid? ByUserId { get; private set; }

    /// <summary>Short preview text.</summary>
    public string Preview { get; private set; } = string.Empty;

    /// <summary>Whether the recipient has read it.</summary>
    public bool IsRead { get; private set; }

    /// <summary>UTC time it was read; null if unread.</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>Creates a mention notification.</summary>
    public static UserNotification Mention(Guid userId, Guid messageId, Guid channelId, Guid byUserId, string preview) =>
        new(userId, NotificationType.Mention, messageId, channelId, byUserId, preview);

    /// <summary>Marks the notification read (idempotent).</summary>
    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
