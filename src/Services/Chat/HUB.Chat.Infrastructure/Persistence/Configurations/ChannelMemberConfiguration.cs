using HUB.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Chat.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="ChannelMember"/>.</summary>
public sealed class ChannelMemberConfiguration : IEntityTypeConfiguration<ChannelMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChannelMember> builder)
    {
        builder.ToTable("channel_members");
        builder.HasKey(m => m.Id);

        // Ids are client-assigned (domain sets Guid in the ctor). Without this, EF treats a new
        // member added to an already-tracked channel as Modified (non-default store-gen key) and
        // issues an UPDATE that affects 0 rows → DbUpdateConcurrencyException. ValueGeneratedNever
        // makes EF correctly INSERT it. Applies to add-member and self-join.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Role).HasConversion<int>();

        // Fast membership lookups + uniqueness of (channel, user).
        builder.HasIndex(m => new { m.ChannelId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
    }
}
