using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Queries.CanJoinChannel;

/// <summary>Handles <see cref="CanJoinChannelQuery"/>.</summary>
/// <remarks>
/// Mirrors the rule <c>ListMessagesHandler</c> enforces — public channels are readable by anyone, private
/// ones only by members. The two must agree: a user allowed to join the realtime group but refused by the
/// message query would see new messages pushed while being unable to load the history, and the reverse
/// would silently drop messages the user is entitled to.
/// </remarks>
/// <param name="db">Chat persistence context.</param>
public sealed class CanJoinChannelHandler(IChatDbContext db) : IRequestHandler<CanJoinChannelQuery, bool>
{
    /// <summary>Decides whether the user may subscribe to the channel.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the channel is public or the user is a member; otherwise false.</returns>
    public async Task<bool> Handle(CanJoinChannelQuery request, CancellationToken ct)
    {
        // Projected rather than loaded: this runs on every join, so it should touch two columns, not
        // hydrate the channel and its whole member collection.
        var channel = await db.Channels
            .AsNoTracking()
            .Where(c => c.Id == request.ChannelId)
            .Select(c => new
            {
                c.IsPrivate,
                IsMember = c.Members.Any(m => m.UserId == request.UserId),
            })
            .FirstOrDefaultAsync(ct);

        // A channel that does not exist is not joinable — treated the same as "not allowed" so the hub
        // never creates a group for a bogus id.
        if (channel is null) return false;

        return !channel.IsPrivate || channel.IsMember;
    }
}
