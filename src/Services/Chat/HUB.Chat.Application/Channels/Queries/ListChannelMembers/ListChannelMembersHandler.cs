using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Queries.ListChannelMembers;

/// <summary>Handles <see cref="ListChannelMembersQuery"/>.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListChannelMembersHandler(IChatDbContext db)
    : IRequestHandler<ListChannelMembersQuery, IReadOnlyList<ChannelMemberDto>>
{
    /// <summary>Returns channel members, applying a privacy check for private channels.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of member DTOs ordered by join date.</returns>
    public async Task<IReadOnlyList<ChannelMemberDto>> Handle(
        ListChannelMembersQuery request, CancellationToken ct)
    {
        var channel = await db.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (channel.IsPrivate && !channel.HasMember(request.ActingUserId))
            throw new ForbiddenException("You are not a member of this channel.");

        return channel.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m => m.ToMemberDto())
            .ToList();
    }
}
