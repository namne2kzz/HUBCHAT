using HUB.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Chat.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="Reaction"/>.</summary>
public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable("reactions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Emoji).IsRequired().HasMaxLength(64);

        // A user reacts at most once with a given emoji per message.
        builder.HasIndex(r => new { r.MessageId, r.UserId, r.Emoji }).IsUnique();
    }
}
