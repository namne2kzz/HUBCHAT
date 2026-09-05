using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Application.Notifications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Application.Notifications.Queries.ListNotifications;

/// <summary>Handles <see cref="ListNotificationsQuery"/> with a no-tracking projection.</summary>
/// <param name="db">Notification persistence context.</param>
public sealed class ListNotificationsHandler(INotificationDbContext db)
    : IRequestHandler<ListNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    /// <summary>Returns up to <c>Limit</c> (1..100) recent notifications.</summary>
    public async Task<IReadOnlyList<NotificationDto>> Handle(ListNotificationsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == request.ActingUserId);
        if (request.UnreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.SourceId, n.ChannelId, n.ByUserId, n.Preview, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }
}
