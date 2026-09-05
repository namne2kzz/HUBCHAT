using HUB.Notification.Application.Common.Exceptions;
using HUB.Notification.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Application.Notifications.Commands.MarkRead;

/// <summary>Handles <see cref="MarkNotificationReadCommand"/>.</summary>
/// <param name="db">Notification persistence context.</param>
public sealed class MarkReadHandler(INotificationDbContext db) : IRequestHandler<MarkNotificationReadCommand>
{
    /// <summary>Marks the notification read; 404 if not found for this user.</summary>
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.ActingUserId, ct)
            ?? throw new NotFoundException("Notification not found.");

        notification.MarkRead();
        await db.SaveChangesAsync(ct);
    }
}
