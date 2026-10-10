using HUB.Notification.Application.Common.Exceptions;
using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HUB.Notification.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the notification bounded context (database <c>hub_notif</c>). Hosts the MassTransit
/// inbox/outbox tables used by the consumer endpoints.
/// </summary>
/// <param name="options">Context options.</param>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options), INotificationDbContext
{
    /// <inheritdoc />
    public DbSet<UserNotification> Notifications => Set<UserNotification>();

    /// <summary>Saves changes, translating a PostgreSQL unique violation into <see cref="UniqueConstraintViolationException"/>.</summary>
    /// <param name="acceptAllChangesOnSuccess">Whether to accept tracked changes after a successful save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of state entries written.</returns>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

        // Inbox: dedupes redelivered messages per consumer, committed in the same transaction as the
        // consumer's writes. Outbox: holds events published while consuming until that transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
