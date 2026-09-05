using HUB.Notification.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace HUB.Notification.Infrastructure.Email;

/// <summary>P2 email sender: logs instead of sending. Replace with an SMTP (MailKit) implementation later.</summary>
/// <param name="logger">Logger.</param>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(Guid toUserId, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("[email:no-op] to={UserId} subject={Subject}", toUserId, subject);
        return Task.CompletedTask;
    }
}
