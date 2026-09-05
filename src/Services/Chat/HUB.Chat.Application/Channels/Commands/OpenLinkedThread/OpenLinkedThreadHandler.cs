using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.OpenLinkedThread;

/// <summary>Handles <see cref="OpenLinkedThreadCommand"/> — find-or-create the linked channel, ensure membership.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class OpenLinkedThreadHandler(IChatDbContext db) : IRequestHandler<OpenLinkedThreadCommand, ChannelDto>
{
    /// <summary>Returns the existing linked thread (adding the caller if needed), or creates a new one.</summary>
    public async Task<ChannelDto> Handle(OpenLinkedThreadCommand request, CancellationToken ct)
    {
        var existing = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.WorkspaceId == request.WorkspaceId && c.LinkExternalId == request.ExternalId, ct);

        if (existing is not null)
        {
            if (!existing.HasMember(request.ActingUserId))
            {
                existing.AddMember(request.ActingUserId);
                await db.SaveChangesAsync(ct);
            }
            return existing.ToDto();
        }

        var name = string.IsNullOrWhiteSpace(request.Title) ? request.ExternalKey : request.Title;
        var channel = Channel.Create(request.WorkspaceId, name, ChannelType.Public, request.ActingUserId);
        channel.LinkTo(request.LinkType, request.ExternalId, request.ExternalKey, request.Url);

        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);
        return channel.ToDto();
    }
}
