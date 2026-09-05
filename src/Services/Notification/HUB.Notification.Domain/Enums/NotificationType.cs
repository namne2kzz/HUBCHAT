namespace HUB.Notification.Domain.Enums;

/// <summary>Kind of notification.</summary>
public enum NotificationType
{
    /// <summary>The user was @mentioned in a message.</summary>
    Mention = 0,
    /// <summary>A direct message was received.</summary>
    DirectMessage = 1,
    /// <summary>System/administrative notice.</summary>
    System = 2,
}
