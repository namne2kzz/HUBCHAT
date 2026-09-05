namespace HUB.Notification.Application.Common.Interfaces;

/// <summary>Sends emails (digest, mention). P2 uses a no-op/log implementation; swap SMTP later.</summary>
public interface IEmailSender
{
    /// <summary>Sends a plain email.</summary>
    /// <param name="toUserId">Recipient user id (resolve address via directory upstream).</param>
    /// <param name="subject">Subject.</param>
    /// <param name="body">Body.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(Guid toUserId, string subject, string body, CancellationToken ct);
}
