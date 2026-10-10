using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Application.Notifications.Commands.SendNotificationEmail;
using HUB.Notification.Domain.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace HUB.Notification.UnitTests;

/// <summary>
/// Covers <c>SendNotificationEmailHandler</c> — the email step split off from storing the notification so a
/// failed send is retried on its own instead of being skipped by the stored-row dedupe.
/// </summary>
public sealed class SendNotificationEmailHandlerTests
{
    [Theory]
    [InlineData(NotificationType.Mention,       "You were mentioned")]
    [InlineData(NotificationType.DirectMessage, "New direct message")]
    [InlineData(NotificationType.System,        "New notification")]
    public async Task SendsToTheRecipientWithASubjectForTheKind(NotificationType type, string subject)
    {
        var email = Substitute.For<IEmailSender>();
        var user  = Guid.NewGuid();

        await new SendNotificationEmailHandler(email)
            .Handle(new SendNotificationEmailCommand(user, type, "preview text"), CancellationToken.None);

        await email.Received(1).SendAsync(user, subject, "preview text", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASendFailureSurfacesSoTheBrokerRetriesIt()
    {
        // Swallowing it would lose the email silently — exactly the old bug this split was made to fix.
        var email = Substitute.For<IEmailSender>();
        email.SendAsync(default, default!, default!, default).ReturnsForAnyArgs(Task.FromException(new IOException("smtp down")));

        await Should.ThrowAsync<IOException>(() => new SendNotificationEmailHandler(email)
            .Handle(new SendNotificationEmailCommand(Guid.NewGuid(), NotificationType.Mention, "x"), CancellationToken.None));
    }
}
