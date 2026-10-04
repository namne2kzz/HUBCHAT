namespace HUB.Chat.Domain.Enums;

/// <summary>Kind of channel.</summary>
public enum ChannelType
{
    /// <summary>Open to any workspace member.</summary>
    Public = 0,
    /// <summary>Invite-only.</summary>
    Private = 1,
    /// <summary>Direct message between exactly two users.</summary>
    Dm = 2,
    /// <summary>Direct message between three or more users.</summary>
    GroupDm = 3,
}
