using HUB.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Chat.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for the <see cref="Channel"/> aggregate.</summary>
public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("channels");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Topic).HasMaxLength(500);
        builder.Property(c => c.Type).HasConversion<int>();

        builder.HasMany(c => c.Members)
            .WithOne()
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Channel.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(c => c.LinkType).HasConversion<int?>();
        builder.Property(c => c.LinkExternalKey).HasMaxLength(64);
        builder.Property(c => c.LinkUrl).HasMaxLength(500);

        // Find-or-create linked discussion threads by (workspace, external resource).
        builder.HasIndex(c => new { c.WorkspaceId, c.LinkExternalId });
        builder.HasIndex(c => c.WorkspaceId);
        builder.HasIndex(c => new { c.WorkspaceId, c.Slug }).IsUnique();
    }
}
