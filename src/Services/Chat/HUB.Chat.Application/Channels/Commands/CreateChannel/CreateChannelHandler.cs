using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Entities;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.CreateChannel;

/// <summary>Handles <see cref="CreateChannelCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class CreateChannelHandler(IChatDbContext db) : IRequestHandler<CreateChannelCommand, ChannelDto>
{
    /// <summary>Creates and persists the channel with the acting user as owner.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created channel as a DTO.</returns>
    public async Task<ChannelDto> Handle(CreateChannelCommand request, CancellationToken ct)
    {
        var channel = Channel.Create(request.WorkspaceId, request.Name, request.Type, request.ActingUserId, request.Topic);

        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);

        return channel.ToDto();
    }
}
