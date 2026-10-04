namespace HUB.Chat.Domain.Enums;

/// <summary>A member's role within a channel.</summary>
public enum ChannelMemberRole
{
    /// <summary>Ordinary participant.</summary>
    Member = 0,
    /// <summary>Can manage members and channel settings.</summary>
    Admin = 1,
    /// <summary>Full control including deletion.</summary>
    Owner = 2,
}
