using HUB.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.Application.Common.Interfaces;

/// <summary>Persistence abstraction for the notification service.</summary>
public interface INotificationDbContext
{
    /// <summary>Notifications table.</summary>
    DbSet<UserNotification> Notifications { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct);
}
