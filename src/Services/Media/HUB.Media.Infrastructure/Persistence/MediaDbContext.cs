using HUB.Media.Application.Common.Interfaces;
using HUB.Media.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace HUB.Media.Infrastructure.Persistence;

/// <summary>EF Core context for the media bounded context (database <c>hub_media</c>). Hosts the MassTransit outbox.</summary>
/// <param name="options">Context options.</param>
public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options), IMediaDbContext
{
    /// <inheritdoc />
    public DbSet<FileObject> Files => Set<FileObject>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        base.OnModelCreating(modelBuilder);
    }
}
