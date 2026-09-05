using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Channels.DTOs;

/// <summary>Read model for a single channel membership (without user profile — caller resolves display name via directory).</summary>
public sealed record ChannelMemberDto(
    Guid UserId,
    ChannelMemberRole Role,
    bool Muted,
    DateTime JoinedAt);
