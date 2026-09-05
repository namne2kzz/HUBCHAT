using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Infrastructure.Persistence;

/// <summary>EF Core context for the notification bounded context (database <c>hub_notif</c>).</summary>
/// <param name="options">Context options.</param>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options), INotificationDbContext
{
    /// <inheritdoc />
    public DbSet<UserNotification> Notifications => Set<UserNotification>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
