using MediatR;

namespace HUB.Media.Application.Files.Commands.CompleteUpload;

/// <summary>Confirms an upload finished; marks scanned and publishes FileUploaded.</summary>
public sealed record CompleteUploadCommand(Guid FileId, Guid ActingUserId) : IRequest;
