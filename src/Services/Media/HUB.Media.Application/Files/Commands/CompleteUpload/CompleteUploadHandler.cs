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
    /// <summary>Marks the file scanned (P2: assumed clean) and publishes FileUploaded atomically — once.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Idempotent: completing an already-completed file is a no-op success, so a client retry does not
    /// publish a second <see cref="FileUploaded"/>. Not race-proof — two truly concurrent completes can both
    /// read Pending; closing that needs an optimistic concurrency token (xmin) on <c>FileObject</c>.
    /// </remarks>
    public async Task Handle(CompleteUploadCommand request, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == request.FileId, ct)
                   ?? throw new NotFoundException();

        // P2: no real AV scan yet — assume clean. Replace with a scan hook before GA.
        if (!file.MarkScanned(clean: true))
            return;

        await events.PublishAsync(
            new FileUploaded(file.Id, file.WorkspaceId, file.StorageKey, file.ContentType, file.SizeBytes), ct);
        await db.SaveChangesAsync(ct);
    }
}
