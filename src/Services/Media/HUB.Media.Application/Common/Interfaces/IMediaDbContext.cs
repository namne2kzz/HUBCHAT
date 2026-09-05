using HUB.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HUB.Media.Application.Common.Interfaces;

/// <summary>Persistence abstraction for the media service.</summary>
public interface IMediaDbContext
{
    /// <summary>File metadata table.</summary>
    DbSet<FileObject> Files { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct);
}
