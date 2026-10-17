using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Queries.SearchMessages;

/// <summary>Handles <see cref="SearchMessagesQuery"/>: full-history text search within one channel.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class SearchMessagesHandler(IChatDbContext db) : IRequestHandler<SearchMessagesQuery, IReadOnlyList<MessageDto>>
{
    /// <summary>Returns matching non-deleted messages (newest first), after checking read access.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching messages, newest first; empty for a blank term.</returns>
    public async Task<IReadOnlyList<MessageDto>> Handle(SearchMessagesQuery request, CancellationToken ct)
    {
        var term = request.Term.Trim();
        if (term.Length == 0) return [];

        await ChannelReadAccess.EnsureCanReadAsync(db, request.ChannelId, request.ActingUserId, ct);

        var limit = Math.Clamp(request.Limit, 1, 50);

        // EF.Functions.ILike → PostgreSQL case-insensitive match. Escape the LIKE wildcards in the user's
        // term so "100%" searches for the literal text, not "100 then anything".
        var pattern = "%" + term.ToLower().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

        var messages = await db.Messages.AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .Where(m => m.ChannelId == request.ChannelId && m.DeletedAt == null
                     && EF.Functions.Like(m.Body.ToLower(), pattern, "\\"))
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        return [.. messages.Select(m => m.ToDto())];
    }
}
