using HUB.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Chat.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for the <see cref="Message"/> aggregate.</summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.Format).HasConversion<int>();

        // Mentions: map the private field as a primitive collection (jsonb on PostgreSQL).
        builder.Ignore(m => m.Mentions);
        builder.Property<List<Guid>>("_mentions")
            .HasColumnName("mentions")
            .HasColumnType("jsonb");

        builder.HasMany(m => m.Reactions)
            .WithOne()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Message.Reactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Attachments)
            .WithOne()
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Message.Attachments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Hot path: newest messages per channel (keyset pagination).
        builder.HasIndex(m => new { m.ChannelId, m.CreatedAt });
        builder.HasIndex(m => m.ParentId);

        // Idempotent send: one message per (author, client key). Filtered so legacy rows / clients that
        // send no key (NULL) never collide. Concurrent retries race on this index, not on a read.
        builder.HasIndex(m => new { m.AuthorId, m.ClientMessageId })
            .IsUnique()
            .HasFilter("\"ClientMessageId\" IS NOT NULL");
    }
}
