using HUB.Media.Application.Files.DTOs;
using MediatR;

namespace HUB.Media.Application.Files.Commands.CreateUploadTicket;

/// <summary>Reserves file metadata and returns a presigned PUT URL for direct client upload.</summary>
public sealed record CreateUploadTicketCommand(
    Guid WorkspaceId,
    Guid? ChannelId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid ActingUserId) : IRequest<UploadTicketDto>;
