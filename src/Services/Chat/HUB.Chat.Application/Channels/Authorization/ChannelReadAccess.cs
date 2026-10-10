using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Authorization;

/// <summary>Who may read a channel's messages: anyone for a public channel, members only otherwise.</summary>
public static class ChannelReadAccess
{
    /// <summary>Throws unless the user may read the channel's messages.</summary>
    /// <param name="db">Chat persistence context.</param>
    /// <param name="channelId">Channel being read.</param>
    /// <param name="userId">The reading user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the user is allowed to read.</returns>
    /// <exception cref="NotFoundException">The channel does not exist.</exception>
    /// <exception cref="ForbiddenException">A non-member reading a private/DM channel.</exception>
    public static async Task EnsureCanReadAsync(IChatDbContext db, Guid channelId, Guid userId, CancellationToken ct)
    {
        var channel = await db.Channels.AsNoTracking()
            .Where(c => c.Id == channelId)
            .Select(c => new { c.Type, IsMember = c.Members.Any(m => m.UserId == userId) })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Channel not found.");

        if (channel.Type != ChannelType.Public && !channel.IsMember)
            throw new ForbiddenException("You are not a member of this channel.");
    }
}
