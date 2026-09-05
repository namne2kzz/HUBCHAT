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

/// <summary>Body format of a message.</summary>
public enum MessageFormat
{
    /// <summary>Plain text.</summary>
    Plain = 0,
    /// <summary>Markdown.</summary>
    Markdown = 1,
}

/// <summary>Kind of external DASHBOARD resource a channel/thread is linked to.</summary>
public enum LinkedResourceType
{
    /// <summary>A DASHBOARD work item (User Story / Task / Bug / TestPlan).</summary>
    WorkItem = 0,
    /// <summary>A DASHBOARD wiki page.</summary>
    WikiPage = 1,
    /// <summary>A DASHBOARD sprint — the channel is the sprint's team discussion space.</summary>
    Sprint = 2,
}
