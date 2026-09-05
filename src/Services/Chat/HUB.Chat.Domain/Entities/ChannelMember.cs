using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Domain.Entities;

/// <summary>Membership of a user in a <see cref="Channel"/> (part of the Channel aggregate).</summary>
public sealed class ChannelMember : Entity
{
    private ChannelMember() { } // EF

    internal ChannelMember(Guid channelId, Guid userId, ChannelMemberRole role)
    {
        ChannelId = channelId;
        UserId    = userId;
        Role      = role;
        JoinedAt  = DateTime.UtcNow;
    }

    /// <summary>Owning channel id.</summary>
    public Guid ChannelId { get; private set; }

    /// <summary>Member user id (from DASHBOARD identity).</summary>
    public Guid UserId { get; private set; }

    /// <summary>Role within the channel.</summary>
    public ChannelMemberRole Role { get; private set; }

    /// <summary>Whether the member muted notifications for this channel.</summary>
    public bool Muted { get; private set; }

    /// <summary>Timestamp of the last message the member has read (for unread counts).</summary>
    public DateTime? LastReadAt { get; private set; }

    /// <summary>When the member joined.</summary>
    public DateTime JoinedAt { get; private set; }

    /// <summary>Marks the channel read up to <paramref name="readAt"/>.</summary>
    /// <param name="readAt">The point in time read up to (usually a message timestamp).</param>
    public void MarkRead(DateTime readAt)
    {
        if (LastReadAt is null || readAt > LastReadAt) LastReadAt = readAt;
    }

    /// <summary>Changes this member's role.</summary>
    /// <param name="role">The new role.</param>
    public void ChangeRole(ChannelMemberRole role) => Role = role;
}
