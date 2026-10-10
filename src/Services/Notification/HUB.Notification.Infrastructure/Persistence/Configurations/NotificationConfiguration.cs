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
        // One notification per (recipient, source, kind). The inbox stops redelivery of the same message;
        // this unique index is the last guard against two different messages for the same mention racing.
        // Filtered: SourceId is optional (system notifications), and NULLs must never collide.
        builder.HasIndex(n => new { n.UserId, n.SourceId, n.Type })
            .IsUnique()
            .HasFilter("\"SourceId\" IS NOT NULL");
    }
}
