using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.AddChannelMember;

/// <summary>
/// Force-adds a user to a channel without requiring them to self-join.
/// Used by the public API (with <paramref name="AddedByUserId"/>) and by the internal API
/// (without — sprint capacity sync). Idempotent — does nothing if the user is already a member.
/// Returns the membership DTO for the added (or already-existing) user.
/// </summary>
/// <param name="ChannelId">Target channel.</param>
/// <param name="UserId">User to add.</param>
/// <param name="AddedByUserId">The user performing the action; null when called internally.</param>
public sealed record AddChannelMemberCommand(Guid ChannelId, Guid UserId, Guid? AddedByUserId = null) : IRequest<ChannelMemberDto>;
