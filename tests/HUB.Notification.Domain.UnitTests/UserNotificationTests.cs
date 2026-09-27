using HUB.Notification.Domain.Entities;
using HUB.Notification.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Notification.Domain.UnitTests;

/// <summary>
/// Covers <c>UserNotification</c>: how a mention notification is built, and the read flag.
/// </summary>
/// <remarks>
/// The read flag is the whole state machine here, and <c>MarkRead</c> is idempotent on purpose.
/// A mention arrives through a RabbitMQ consumer, and the repository's rules require consumers to
/// tolerate redelivery — so anything downstream that marks one read has to survive being called twice.
/// </remarks>
public sealed class UserNotificationTests
{
    private static UserNotification NewMention() =>
        UserNotification.Mention(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hello there");

    [Fact]
    public void AMentionCarriesEverythingNeededToLinkBackToTheMessage()
    {
        var recipient = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var author    = Guid.NewGuid();

        var notification = UserNotification.Mention(recipient, messageId, channelId, author, "you were mentioned");

        notification.UserId.ShouldBe(recipient);
        notification.Type.ShouldBe(NotificationType.Mention);

        // SourceId is the message. Without it the notification cannot deep-link, which is the only thing
        // a person does with one.
        notification.SourceId.ShouldBe(messageId);
        notification.ChannelId.ShouldBe(channelId);
        notification.ByUserId.ShouldBe(author);
        notification.Preview.ShouldBe("you were mentioned");
    }

    [Fact]
    public void ANewNotificationIsUnread()
    {
        var notification = NewMention();

        notification.IsRead.ShouldBeFalse();
        notification.ReadAt.ShouldBeNull();
    }

    [Fact]
    public void ANewNotificationIsTimestamped()
    {
        // The list is ordered by this, so an unset value would sort the newest notification arbitrarily.
        NewMention().CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public void MarkingReadSetsTheFlagAndTheTime()
    {
        var notification = NewMention();

        notification.MarkRead();

        notification.IsRead.ShouldBeTrue();
        notification.ReadAt.ShouldNotBeNull();
    }

    [Fact]
    public void MarkingReadTwiceKeepsTheFirstTime()
    {
        var notification = NewMention();

        notification.MarkRead();
        var firstRead = notification.ReadAt;

        notification.MarkRead();

        // "Mark all read" and a per-item click can both land on the same row, and a redelivered message
        // can repeat either. The first time is the true one.
        notification.ReadAt.ShouldBe(firstRead);
        notification.IsRead.ShouldBeTrue();
    }

    [Fact]
    public void AnEmptyPreviewIsAllowed()
    {
        // UserMentionedConsumer currently passes Preview: string.Empty — the event carries no preview
        // text of its own. Pinned so the empty case is known to be reachable in production rather than
        // only in a test.
        UserNotification.Mention(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), string.Empty)
            .Preview.ShouldBeEmpty();
    }

    [Fact]
    public void TwoNotificationsGetDifferentIdentities()
    {
        NewMention().Id.ShouldNotBe(NewMention().Id);
    }
}
