using MediatR;

namespace HUB.Chat.Application.Channels.Commands.RemoveChannelMember;

/// <summary>
/// Removes a user from a channel.
/// Used by the internal API when a capacity member is removed from a sprint that has a linked channel.
/// Silently succeeds if the user is not a member.
/// </summary>
/// <param name="ChannelId">Target channel.</param>
/// <param name="UserId">User to remove.</param>
public sealed record RemoveChannelMemberCommand(Guid ChannelId, Guid UserId) : IRequest;
