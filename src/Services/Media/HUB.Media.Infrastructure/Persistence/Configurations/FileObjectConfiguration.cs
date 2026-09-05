using HUB.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HUB.Media.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="FileObject"/>.</summary>
public sealed class FileObjectConfiguration : IEntityTypeConfiguration<FileObject>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileObject> builder)
    {
        builder.ToTable("files");
        builder.HasKey(f => f.Id);
        builder.Ignore(f => f.Bucket);
        builder.Ignore(f => f.IsDownloadable);
        builder.Property(f => f.FileName).IsRequired().HasMaxLength(260);
        builder.Property(f => f.ContentType).IsRequired().HasMaxLength(160);
        builder.Property(f => f.StorageKey).IsRequired().HasMaxLength(320);
        builder.Property(f => f.ScanStatus).HasConversion<int>();
        builder.HasIndex(f => new { f.WorkspaceId, f.ChannelId });
    }
}
