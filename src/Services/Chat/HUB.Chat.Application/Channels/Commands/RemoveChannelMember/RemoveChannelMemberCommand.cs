using MediatR;

namespace HUB.Chat.Application.Channels.Commands.RemoveChannelMember;

/// <summary>
/// Removes a user from a channel. Silently succeeds if the user is not a member.
/// Used by the public API (with <paramref name="ActingUserId"/>, permission-checked) and by the internal
/// API (without — sprint capacity sync, trusted service-to-service call).
/// </summary>
/// <param name="ChannelId">Target channel.</param>
/// <param name="UserId">User to remove.</param>
/// <param name="ActingUserId">The user performing the removal; null only for the internal API.</param>
public sealed record RemoveChannelMemberCommand(Guid ChannelId, Guid UserId, Guid? ActingUserId = null) : IRequest;
