using HUB.Notification.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Application.Notifications.Commands.MarkAllRead;

/// <summary>Handles <see cref="MarkAllReadCommand"/>.</summary>
/// <param name="db">Notification persistence context.</param>
public sealed class MarkAllReadHandler(INotificationDbContext db) : IRequestHandler<MarkAllReadCommand>
{
    /// <summary>Marks every unread notification of the user as read.</summary>
    public async Task Handle(MarkAllReadCommand request, CancellationToken ct)
    {
        var unread = await db.Notifications
            .Where(n => n.UserId == request.ActingUserId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in unread) n.MarkRead();
        await db.SaveChangesAsync(ct);
    }
}
