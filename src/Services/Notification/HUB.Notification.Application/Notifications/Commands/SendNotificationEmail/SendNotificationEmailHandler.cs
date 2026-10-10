using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Domain.Enums;
using MediatR;

namespace HUB.Notification.Application.Notifications.Commands.SendNotificationEmail;

/// <summary>Handles <see cref="SendNotificationEmailCommand"/>.</summary>
/// <param name="email">Email sender.</param>
public sealed class SendNotificationEmailHandler(IEmailSender email) : IRequestHandler<SendNotificationEmailCommand>
{
    /// <summary>Sends the notification email.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the email is handed to the sender.</returns>
    /// <remarks>
    /// Failures throw so the broker retries this step alone — the notification itself is already stored.
    /// At-least-once: if sending succeeds but the consume does not commit, the email can go out twice; an
    /// email cannot be part of the database transaction.
    /// </remarks>
    public Task Handle(SendNotificationEmailCommand request, CancellationToken ct) =>
        email.SendAsync(request.UserId, Subject(request.Type), request.Preview, ct);

    private static string Subject(NotificationType type) => type switch
    {
        NotificationType.Mention       => "You were mentioned",
        NotificationType.DirectMessage => "New direct message",
        _                              => "New notification",
    };
}
