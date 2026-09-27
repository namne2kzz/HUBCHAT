using HUB.Notification.Application.Common.Exceptions;
using HUB.Notification.Application.Notifications.Commands.MarkAllRead;
using HUB.Notification.Application.Notifications.Commands.MarkRead;
using HUB.Notification.Application.Notifications.Queries.ListNotifications;
using HUB.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Notification.UnitTests;

/// <summary>
/// Covers the notification read and list handlers, with the per-user scoping as the main subject.
/// </summary>
/// <remarks>
/// Every one of these handlers filters on <c>ActingUserId</c>, and that filter is the only thing keeping
/// one person's notifications out of another's. There is no channel membership or role check to fall back
/// on here — a missing <c>where</c> would expose the preview text of mentions in channels the caller
/// cannot even see.
/// </remarks>
public sealed class NotificationHandlerTests
{
    private static UserNotification MentionFor(Guid user, string preview = "hello") =>
        UserNotification.Mention(user, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), preview);

    // ── Marking one read ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkingOwnNotificationReadSucceeds()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var user         = Guid.NewGuid();
        var notification = MentionFor(user);
        lease.Context.Notifications.Add(notification);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await new MarkReadHandler(lease.Context)
            .Handle(new MarkNotificationReadCommand(notification.Id, user), CancellationToken.None);

        await using var verify = lease.NewContext();
        (await verify.Notifications.AsNoTracking().SingleAsync()).IsRead.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkingSomebodyElsesNotificationReadIsNotFound()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var owner        = Guid.NewGuid();
        var notification = MentionFor(owner);
        lease.Context.Notifications.Add(notification);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // 404 rather than 403, deliberately: telling an outsider "that exists but is not yours" confirms
        // the id is real. Here the row simply does not exist as far as they are concerned.
        await Should.ThrowAsync<NotFoundException>(() => new MarkReadHandler(lease.Context)
            .Handle(new MarkNotificationReadCommand(notification.Id, Guid.NewGuid()), CancellationToken.None));

        await using var verify = lease.NewContext();
        (await verify.Notifications.AsNoTracking().SingleAsync())
            .IsRead.ShouldBeFalse("the owner's notification must be untouched");
    }

    [Fact]
    public async Task MarkingAMissingNotificationIsNotFound()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new MarkReadHandler(lease.Context)
            .Handle(new MarkNotificationReadCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task MarkingReadTwiceKeepsTheFirstTimestamp()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var user         = Guid.NewGuid();
        var notification = MentionFor(user);
        lease.Context.Notifications.Add(notification);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new MarkReadHandler(lease.Context);
        var command = new MarkNotificationReadCommand(notification.Id, user);

        await handler.Handle(command, CancellationToken.None);
        var firstRead = notification.ReadAt;

        await handler.Handle(command, CancellationToken.None);

        // The click can arrive twice — from two tabs, or a retried request. The first time is the true one.
        notification.ReadAt.ShouldBe(firstRead);
    }

    // ── Marking all read ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkingAllReadOnlyTouchesTheCallersNotifications()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var caller   = Guid.NewGuid();
        var somebody = Guid.NewGuid();

        lease.Context.Notifications.AddRange(
            MentionFor(caller), MentionFor(caller), MentionFor(somebody));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await new MarkAllReadHandler(lease.Context)
            .Handle(new MarkAllReadCommand(caller), CancellationToken.None);

        await using var verify = lease.NewContext();

        (await verify.Notifications.AsNoTracking().Where(n => n.UserId == caller).ToListAsync())
            .ShouldAllBe(n => n.IsRead);

        // A "mark all read" that forgot its where clause would clear everybody's badge at once.
        (await verify.Notifications.AsNoTracking().SingleAsync(n => n.UserId == somebody))
            .IsRead.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkingAllReadWithNothingUnreadIsHarmless()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        await new MarkAllReadHandler(lease.Context)
            .Handle(new MarkAllReadCommand(Guid.NewGuid()), CancellationToken.None);

        await using var verify = lease.NewContext();
        (await verify.Notifications.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task MarkingAllReadLeavesAlreadyReadOnesAlone()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var user  = Guid.NewGuid();
        var older = MentionFor(user);
        older.MarkRead();
        var readAt = older.ReadAt;

        lease.Context.Notifications.AddRange(older, MentionFor(user));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await new MarkAllReadHandler(lease.Context)
            .Handle(new MarkAllReadCommand(user), CancellationToken.None);

        // The handler filters on !IsRead, so an already-read notification keeps the time it was actually
        // read rather than being restamped to now.
        older.ReadAt.ShouldBe(readAt);
    }

    // ── Listing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListingReturnsOnlyTheCallersNotifications()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var caller = Guid.NewGuid();
        lease.Context.Notifications.AddRange(
            MentionFor(caller, "mine"), MentionFor(Guid.NewGuid(), "theirs"));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListNotificationsHandler(lease.Context)
            .Handle(new ListNotificationsQuery(caller, UnreadOnly: false, Limit: 50), CancellationToken.None);

        // The preview text is the payload that leaks here — it carries the message body of a mention in a
        // channel the caller may have no access to at all.
        result.ShouldHaveSingleItem().Preview.ShouldBe("mine");
    }

    [Fact]
    public async Task ListingUnreadOnlyExcludesReadNotifications()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var user = Guid.NewGuid();
        var read = MentionFor(user, "already seen");
        read.MarkRead();

        lease.Context.Notifications.AddRange(read, MentionFor(user, "new"));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListNotificationsHandler(lease.Context)
            .Handle(new ListNotificationsQuery(user, UnreadOnly: true, Limit: 50), CancellationToken.None);

        result.ShouldHaveSingleItem().Preview.ShouldBe("new");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(5000, 100)]
    public async Task TheLimitIsClamped(int requested, int expected)
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var user = Guid.NewGuid();
        for (var i = 0; i < 105; i++) lease.Context.Notifications.Add(MentionFor(user));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListNotificationsHandler(lease.Context)
            .Handle(new ListNotificationsQuery(user, UnreadOnly: false, Limit: requested), CancellationToken.None);

        // A zero or negative limit returning an empty list would read as "no notifications"; an unbounded
        // one lets a client pull the whole history in a request.
        result.Count.ShouldBe(expected);
    }

    [Fact]
    public async Task ListingReturnsNewestFirst()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var user = Guid.NewGuid();
        lease.Context.Notifications.AddRange(MentionFor(user, "first"), MentionFor(user, "second"));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListNotificationsHandler(lease.Context)
            .Handle(new ListNotificationsQuery(user, UnreadOnly: false, Limit: 50), CancellationToken.None);

        result.Select(n => n.CreatedAt).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task ListingWithNoNotificationsReturnsAnEmptyList()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var result = await new ListNotificationsHandler(lease.Context)
            .Handle(new ListNotificationsQuery(Guid.NewGuid(), UnreadOnly: false, Limit: 50), CancellationToken.None);

        result.ShouldBeEmpty();
    }
}
