using HUB.Media.Application.Common.Exceptions;
using HUB.Media.Application.Common.Interfaces;
using HUB.Media.Application.Files.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Media.Application.Files.Queries.GetDownloadUrl;

/// <summary>Handles <see cref="GetDownloadUrlQuery"/>.</summary>
/// <param name="db">Media persistence context.</param>
/// <param name="storage">Object storage (MinIO).</param>
public sealed class GetDownloadUrlHandler(IMediaDbContext db, IObjectStorage storage)
    : IRequestHandler<GetDownloadUrlQuery, DownloadUrlDto>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    /// <summary>Returns a presigned GET URL; 404 if missing, 409 if not yet scanned clean.</summary>
    public async Task<DownloadUrlDto> Handle(GetDownloadUrlQuery request, CancellationToken ct)
    {
        var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FileId, ct)
                   ?? throw new NotFoundException();

        if (!file.IsDownloadable)
            throw new ConflictException("File is not available for download yet (scan pending or failed).");

        var url = await storage.CreatePresignedGetUrlAsync(file.Bucket, file.StorageKey, Ttl, ct);
        return new DownloadUrlDto(url, (int)Ttl.TotalSeconds);
    }
}
