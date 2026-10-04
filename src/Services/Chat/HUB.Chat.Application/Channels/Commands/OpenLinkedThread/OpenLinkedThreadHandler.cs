using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
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
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The linked channel as seen by the caller.</returns>
    /// <remarks>
    /// Two users opening the same resource at once both miss the lookup and both insert; the unique index
    /// (WorkspaceId, LinkType, LinkExternalId) lets one win, and the loser re-runs the "existing" path
    /// against the winner — so it still ends up a member of the one thread.
    /// </remarks>
    public async Task<ChannelDto> Handle(OpenLinkedThreadCommand request, CancellationToken ct)
    {
        if (await FindLinkedAsync(request, ct) is { } existing)
            return await JoinExistingAsync(existing, request, ct);

        var name = string.IsNullOrWhiteSpace(request.Title) ? request.ExternalKey : request.Title;
        var channel = Channel.Create(request.WorkspaceId, name, ChannelType.Public, request.ActingUserId);
        channel.LinkTo(request.LinkType, request.ExternalId, request.ExternalKey, request.Url);

        db.Channels.Add(channel);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // Drop the losing insert (channel + its owner member) so the membership save below does not retry it.
            db.DiscardChanges();

            // Null here means the clash was something else (e.g. slug) — not ours to resolve.
            var winner = await FindLinkedAsync(request, ct);
            if (winner is null) throw;
            return await JoinExistingAsync(winner, request, ct);
        }

        return channel.ToDto(request.ActingUserId);
    }

    private Task<Channel?> FindLinkedAsync(OpenLinkedThreadCommand request, CancellationToken ct) =>
        db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c =>
                c.WorkspaceId    == request.WorkspaceId &&
                c.LinkType       == request.LinkType    &&
                c.LinkExternalId == request.ExternalId, ct);

    private async Task<ChannelDto> JoinExistingAsync(Channel existing, OpenLinkedThreadCommand request, CancellationToken ct)
    {
        if (!existing.HasMember(request.ActingUserId))
        {
            existing.AddMember(request.ActingUserId);
            await db.SaveChangesAsync(ct);
        }
        return existing.ToDto(request.ActingUserId);
    }
}
