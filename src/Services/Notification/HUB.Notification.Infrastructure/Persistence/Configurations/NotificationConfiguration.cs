using HUB.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Notification.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="Notification"/>.</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<int>();
        builder.Property(n => n.Preview).HasMaxLength(280);

        // Hot path: unread list per user.
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.IsRead });
        // Idempotency lookup for consumer dedup.
        builder.HasIndex(n => new { n.UserId, n.SourceId, n.Type });
    }
}
