using HUB.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Chat.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="Attachment"/> (part of the Message aggregate).</summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind).HasConversion<int>();
        builder.Property(a => a.Url).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(400);
        builder.Property(a => a.Mime).IsRequired().HasMaxLength(200);

        builder.HasIndex(a => a.MessageId);
    }
}
