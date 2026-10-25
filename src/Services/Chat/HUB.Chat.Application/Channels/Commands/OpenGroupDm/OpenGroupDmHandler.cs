using System.Security.Cryptography;
using System.Text;
using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.OpenGroupDm;

/// <summary>Handles <see cref="OpenGroupDmCommand"/>: find-or-create a group DM by its exact member set.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class OpenGroupDmHandler(IChatDbContext db) : IRequestHandler<OpenGroupDmCommand, ChannelDto>
{
    /// <summary>Returns the existing group DM for this exact set of people, or creates one.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The group DM channel DTO.</returns>
    /// <remarks>
    /// The group's identity is its member set, so the name is a deterministic hash of the sorted member
    /// ids. That makes reopening the same group idempotent (unique <c>(WorkspaceId, Slug)</c>), and a
    /// concurrent first-open resolves to one channel: the slower insert hits the unique index and re-reads.
    /// </remarks>
    public async Task<ChannelDto> Handle(OpenGroupDmCommand request, CancellationToken ct)
    {
        // Canonical member set = the caller plus the distinct targets, sorted for a stable identity.
        var members = request.MemberIds.Append(request.ActingUserId).Distinct().OrderBy(id => id).ToList();

        if (await FindAsync(members, ct) is { } existing)
            return existing.ToDto(request.ActingUserId);

        var channel = Channel.Create(request.WorkspaceId, GroupName(members), ChannelType.GroupDm, request.ActingUserId);
        foreach (var id in members.Where(id => id != request.ActingUserId))
            channel.AddMember(id, ChannelMemberRole.Member);

        db.Channels.Add(channel);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // Someone opened the same group at the same moment — the deterministic name collided. Return theirs.
            db.DiscardChanges();
            var winner = await FindAsync(members, ct);
            if (winner is null) throw;
            return winner.ToDto(request.ActingUserId);
        }

        return channel.ToDto(request.ActingUserId);
    }

    /// <summary>Finds a group DM whose member set is exactly <paramref name="members"/>.</summary>
    private async Task<Channel?> FindAsync(List<Guid> members, CancellationToken ct)
    {
        // Narrow in SQL to group DMs of the right size that contain the caller, then match the exact set in
        // memory (set equality does not translate cleanly across providers).
        var candidates = await db.Channels
            .Include(c => c.Members)
            .Where(c => c.Type == ChannelType.GroupDm && c.Members.Count == members.Count
                        && c.Members.Any(m => m.UserId == members[0]))
            .ToListAsync(ct);

        var target = members.ToHashSet();
        return candidates.FirstOrDefault(c => c.Members.Select(m => m.UserId).ToHashSet().SetEquals(target));
    }

    /// <summary>
    /// A deterministic, collision-resistant channel name from the sorted member ids. Not shown to users —
    /// the client renders a group DM from its members — but it must be unique and ≤ the name length limit.
    /// </summary>
    private static string GroupName(IEnumerable<Guid> sortedMembers)
    {
        var joined = string.Join(",", sortedMembers);
        var hash   = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return "gdm-" + Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}
