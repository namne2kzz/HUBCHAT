using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Domain.Entities;
using HUB.Notification.Domain.Enums;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Application.Notifications.Commands.CreateNotification;

/// <summary>Handles <see cref="CreateMentionNotificationCommand"/> (idempotent per user+message).</summary>
/// <param name="db">Notification persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class CreateMentionNotificationHandler(INotificationDbContext db, IIntegrationEventPublisher events)
    : IRequestHandler<CreateMentionNotificationCommand>
{
    /// <summary>Stores the notification (skipping duplicates) and announces it via <see cref="NotificationCreated"/>.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the notification is stored.</returns>
    /// <remarks>
    /// <para>
    /// Redelivery of the same broker message is stopped earlier, by the inbox on the consumer endpoint. The
    /// lookup here covers a <i>different</i> message describing the same mention; the unique index
    /// (UserId, SourceId, Type) is the last guard if two of those race.
    /// </para>
    /// <para>
    /// No email is sent here any more. It used to go out after <c>SaveChanges</c>: when it failed, the retry
    /// found the row already stored, returned early, and the email was lost for good. Email and realtime push
    /// now hang off <see cref="NotificationCreated"/>, which the outbox releases only if this insert commits,
    /// and each is retried on its own.
    /// </para>
    /// </remarks>
    public async Task Handle(CreateMentionNotificationCommand request, CancellationToken ct)
    {
        var exists = await db.Notifications.AsNoTracking().AnyAsync(
            n => n.UserId == request.UserId && n.SourceId == request.MessageId && n.Type == NotificationType.Mention, ct);
        if (exists) return;

        var notification = UserNotification.Mention(
            request.UserId, request.MessageId, request.ChannelId, request.ByUserId, request.Preview);
        db.Notifications.Add(notification);

        await events.PublishAsync(new NotificationCreated(
            notification.Id, notification.UserId, (int)notification.Type, notification.SourceId,
            notification.ChannelId, notification.ByUserId, notification.Preview, notification.CreatedAt), ct);

        await db.SaveChangesAsync(ct);
    }
}
