namespace HUB.Shared.Contracts.Events;

/// <summary>
/// Published by notification-service (outbox) once a notification is stored. Consumed by notification-service
/// itself to send the email, and by realtime-service to push it to the recipient's open sessions.
/// </summary>
/// <remarks>
/// Carries the full display shape so realtime can push without calling back into notification-service.
/// <see cref="Type"/> is the numeric value of notification-service's <c>NotificationType</c> (wire format:
/// only append values, never renumber) — the contract cannot reference that service's domain.
/// </remarks>
/// <param name="NotificationId">The stored notification's id.</param>
/// <param name="UserId">Recipient.</param>
/// <param name="Type">Notification kind (0 = Mention, 1 = DirectMessage, 2 = System).</param>
/// <param name="SourceId">Source entity id (e.g. message id).</param>
/// <param name="ChannelId">Related channel, if any.</param>
/// <param name="ByUserId">User who triggered it, if any.</param>
/// <param name="Preview">Short preview text.</param>
/// <param name="CreatedAt">UTC time the notification was created.</param>
public sealed record NotificationCreated(
    Guid NotificationId,
    Guid UserId,
    int Type,
    Guid? SourceId,
    Guid? ChannelId,
    Guid? ByUserId,
    string Preview,
    DateTime CreatedAt) : IntegrationEvent;
