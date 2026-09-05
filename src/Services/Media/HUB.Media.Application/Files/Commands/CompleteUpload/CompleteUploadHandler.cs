using HUB.Media.Application.Common.Exceptions;
using HUB.Media.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Media.Application.Files.Commands.CompleteUpload;

/// <summary>Handles <see cref="CompleteUploadCommand"/>.</summary>
/// <param name="db">Media persistence context.</param>
/// <param name="events">Integration event publisher (outbox).</param>
public sealed class CompleteUploadHandler(IMediaDbContext db, IIntegrationEventPublisher events)
    : IRequestHandler<CompleteUploadCommand>
{
    /// <summary>Marks the file scanned (P2: assumed clean) and publishes FileUploaded atomically.</summary>
    public async Task Handle(CompleteUploadCommand request, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == request.FileId, ct)
                   ?? throw new NotFoundException();

        // P2: no real AV scan yet — assume clean. Replace with a scan hook before GA.
        file.MarkScanned(clean: true);

        await events.PublishAsync(
            new FileUploaded(file.Id, file.WorkspaceId, file.StorageKey, file.ContentType, file.SizeBytes), ct);
        await db.SaveChangesAsync(ct);
    }
}
