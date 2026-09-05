using HUB.Media.Application.Common.Interfaces;
using HUB.Media.Application.Files.DTOs;
using HUB.Media.Domain.Entities;
using MediatR;

namespace HUB.Media.Application.Files.Commands.CreateUploadTicket;

/// <summary>Handles <see cref="CreateUploadTicketCommand"/>.</summary>
/// <param name="db">Media persistence context.</param>
/// <param name="storage">Object storage (MinIO).</param>
public sealed class CreateUploadTicketHandler(IMediaDbContext db, IObjectStorage storage)
    : IRequestHandler<CreateUploadTicketCommand, UploadTicketDto>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    /// <summary>Saves pending metadata, ensures the bucket, and returns a presigned PUT URL.</summary>
    public async Task<UploadTicketDto> Handle(CreateUploadTicketCommand request, CancellationToken ct)
    {
        var file = FileObject.Create(
            request.WorkspaceId, request.ChannelId, request.FileName, request.ContentType, request.SizeBytes, request.ActingUserId);

        await storage.EnsureBucketAsync(file.Bucket, ct);
        db.Files.Add(file);
        await db.SaveChangesAsync(ct);

        var url = await storage.CreatePresignedPutUrlAsync(file.Bucket, file.StorageKey, Ttl, ct);
        return new UploadTicketDto(file.Id, url, file.StorageKey, (int)Ttl.TotalSeconds);
    }
}
