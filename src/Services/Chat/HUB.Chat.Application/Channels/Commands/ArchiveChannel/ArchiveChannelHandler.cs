using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.ArchiveChannel;

/// <summary>Handles <see cref="ArchiveChannelCommand"/>: archive/reopen a channel; idempotent; announced.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
/// <param name="permissions">Caller workspace permissions (for the moderator check on the public path).</param>
public sealed class ArchiveChannelHandler(IChatDbContext db, IIntegrationEventPublisher events, IWorkspacePermissions permissions)
    : IRequestHandler<ArchiveChannelCommand>
{
    /// <summary>Archives/reopens the channel after a permission check (public path only), and announces it.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the state is stored.</returns>
    /// <exception cref="NotFoundException">The channel does not exist.</exception>
    /// <exception cref="ForbiddenException">A public-API caller who is not a channel moderator.</exception>
    public async Task Handle(ArchiveChannelCommand request, CancellationToken ct)
    {
        var channel = await db.Channels.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        // Only Owner/Admin (or ManageChannels) may archive/reopen. Internal sprint close carries no actor.
        if (request.ActingUserId is { } actor)
            await ChannelMemberManagement.EnsureCanManageMembersAsync(channel, actor, permissions, ct);

        var changed = request.Archive ? channel.Archive() : channel.Unarchive();
        if (!changed) return;

        await events.PublishAsync(new ChannelArchivedChanged(channel.Id, channel.IsArchived), ct);
        await db.SaveChangesAsync(ct);
    }
}
