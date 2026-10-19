using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Channels.DTOs;

/// <summary>Read model for a single channel membership (without user profile — caller resolves display name via directory).</summary>
/// <param name="UserId">The member's user id.</param>
/// <param name="Role">The member's role.</param>
/// <param name="Muted">Whether the member muted the channel.</param>
/// <param name="JoinedAt">When the member joined.</param>
/// <param name="LastReadAt">The member's read point — drives the DM "Seen" receipt (CF-15); null if never read.</param>
public sealed record ChannelMemberDto(
    Guid UserId,
    ChannelMemberRole Role,
    bool Muted,
    DateTime JoinedAt,
    DateTime? LastReadAt = null);
