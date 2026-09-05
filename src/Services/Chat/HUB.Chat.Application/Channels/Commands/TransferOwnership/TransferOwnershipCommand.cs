using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.TransferOwnership;

/// <summary>Transfers channel ownership to another member. Caller must be the current Owner.</summary>
/// <param name="ChannelId">Channel whose ownership is transferred.</param>
/// <param name="NewOwnerUserId">Member to promote to Owner.</param>
/// <param name="ActingUserId">The current owner performing the transfer.</param>
public sealed record TransferOwnershipCommand(Guid ChannelId, Guid NewOwnerUserId, Guid ActingUserId)
    : IRequest<ChannelDto>;
