using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Domain.Entities;
using HUB.Notification.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Application.Notifications.Commands.CreateNotification;

/// <summary>Handles <see cref="CreateMentionNotificationCommand"/> (idempotent per user+message).</summary>
/// <param name="db">Notification persistence context.</param>
/// <param name="email">Email sender (no-op in P2).</param>
public sealed class CreateMentionNotificationHandler(INotificationDbContext db, IEmailSender email)
    : IRequestHandler<CreateMentionNotificationCommand>
{
    /// <summary>Creates an in-app notification (skips duplicates) and fires a best-effort email.</summary>
    public async Task Handle(CreateMentionNotificationCommand request, CancellationToken ct)
    {
        // Idempotency: at-least-once delivery may replay the event.
        var exists = await db.Notifications.AsNoTracking().AnyAsync(
            n => n.UserId == request.UserId && n.SourceId == request.MessageId && n.Type == NotificationType.Mention, ct);
        if (exists) return;

        db.Notifications.Add(UserNotification.Mention(
            request.UserId, request.MessageId, request.ChannelId, request.ByUserId, request.Preview));
        await db.SaveChangesAsync(ct);

        await email.SendAsync(request.UserId, "You were mentioned", request.Preview, ct);
    }
}
