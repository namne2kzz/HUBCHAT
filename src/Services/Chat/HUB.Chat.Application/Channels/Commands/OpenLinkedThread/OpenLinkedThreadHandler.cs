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

        // The name maps to a unique (WorkspaceId, Slug). Another channel may already own that slug (a thread
        // for a different resource with the same title, or a user-made channel), so on a slug clash we
        // disambiguate with a numeric suffix rather than return a 409 the caller cannot act on.
        // Leave headroom for a " (NN)" suffix within the 100-char name limit.
        var rawBase  = string.IsNullOrWhiteSpace(request.Title) ? request.ExternalKey : request.Title;
        var baseName = rawBase.Length > 90 ? rawBase[..90].TrimEnd() : rawBase;

        for (var attempt = 0; attempt <= MaxSlugAttempts; attempt++)
        {
            var name = attempt == 0 ? baseName : $"{baseName} ({attempt + 1})";
            var channel = Channel.Create(request.WorkspaceId, name, ChannelType.Public, request.ActingUserId);
            channel.LinkTo(request.LinkType, request.ExternalId, request.ExternalKey, request.Url);

            db.Channels.Add(channel);
            try
            {
                await db.SaveChangesAsync(ct);
                return channel.ToDto(request.ActingUserId);
            }
            catch (UniqueConstraintViolationException)
            {
                // Drop the losing insert (channel + its owner member) so a retry doesn't resubmit it.
                db.DiscardChanges();

                // A concurrent call created THIS resource's thread first (unique link index) — join it.
                if (await FindLinkedAsync(request, ct) is { } winner)
                    return await JoinExistingAsync(winner, request, ct);

                // Otherwise it was a (WorkspaceId, Slug) name collision — try the next suffix.
            }
        }

        // Exhausted suffixes: fall back to a slug that cannot collide.
        var unique = Channel.Create(
            request.WorkspaceId, $"{baseName} ({Guid.NewGuid():N})"[..Math.Min(baseName.Length + 10, 100)],
            ChannelType.Public, request.ActingUserId);
        unique.LinkTo(request.LinkType, request.ExternalId, request.ExternalKey, request.Url);
        db.Channels.Add(unique);
        await db.SaveChangesAsync(ct);
        return unique.ToDto(request.ActingUserId);
    }

    /// <summary>Most "(N)" suffixes tried before falling back to a guid-suffixed name.</summary>
    private const int MaxSlugAttempts = 20;

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
