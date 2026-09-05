using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Queries.ListChannelMembers;

/// <summary>Returns the members of a channel. For private channels the acting user must be a member.</summary>
/// <param name="ChannelId">Channel to list members of.</param>
/// <param name="ActingUserId">Caller (used for access check on private channels).</param>
public sealed record ListChannelMembersQuery(Guid ChannelId, Guid ActingUserId)
    : IRequest<IReadOnlyList<ChannelMemberDto>>;
