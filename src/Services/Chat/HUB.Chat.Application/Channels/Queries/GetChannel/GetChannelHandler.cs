using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Queries.GetChannel;

/// <summary>Handles <see cref="GetChannelQuery"/> with a no-tracking projection.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class GetChannelHandler(IChatDbContext db) : IRequestHandler<GetChannelQuery, ChannelDto>
{
    /// <summary>Returns the channel if public or the caller is a member; otherwise 404/403.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The channel DTO.</returns>
    public async Task<ChannelDto> Handle(GetChannelQuery request, CancellationToken ct)
    {
        var channel = await db.Channels
            .AsNoTracking()
            .Where(c => c.Id == request.ChannelId)
            .Select(c => new
            {
                Dto = new ChannelDto(
                    c.Id, c.WorkspaceId, c.Name, c.Slug, c.Type, c.Topic, c.IsPrivate,
                    c.IsArchived, c.Members.Count, c.CreatedAt, c.LinkType, c.LinkExternalKey, c.LinkUrl,
                    IsMember: c.Members.Any(m => m.UserId == request.ActingUserId),
                    OtherUserId: (c.Type == ChannelType.Dm || c.Type == ChannelType.GroupDm)
                        ? c.Members.Where(m => m.UserId != request.ActingUserId).Select(m => (Guid?)m.UserId).FirstOrDefault()
                        : null,
                    MyRole: c.Members.Where(m => m.UserId == request.ActingUserId)
                        .Select(m => (ChannelMemberRole?)m.Role).FirstOrDefault()),
                c.Type,
                IsMember = c.Members.Any(m => m.UserId == request.ActingUserId),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Channel not found.");

        if (channel.Type != ChannelType.Public && !channel.IsMember)
            throw new ForbiddenException("You are not a member of this channel.");

        return channel.Dto;
    }
}
