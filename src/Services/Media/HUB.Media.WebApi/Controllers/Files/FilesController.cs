using HUB.Media.Application.Files.Commands.CompleteUpload;
using HUB.Media.Application.Files.Commands.CreateUploadTicket;
using HUB.Media.Application.Files.DTOs;
using HUB.Media.Application.Files.Queries.GetDownloadUrl;
using HUB.Media.WebApi.Controllers.Files.Requests;
using HUB.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HUB.Media.WebApi.Controllers.Files;

/// <summary>File attachment endpoints (presigned upload/download via MinIO).</summary>
/// <param name="mediator">MediatR sender.</param>
/// <param name="currentUser">The authenticated user.</param>
[ApiController]
[Route("api/v1/files")]
[Authorize]
public sealed class FilesController(ISender mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Requests an upload ticket (presigned PUT URL). Client uploads bytes directly to MinIO.</summary>
    /// <param name="request">File details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the upload ticket.</returns>
    [HttpPost]
    [ProducesResponseType<UploadTicketDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateUploadTicketRequest request, CancellationToken ct)
    {
        var dto = await mediator.Send(new CreateUploadTicketCommand(
            request.WorkspaceId, request.ChannelId, request.FileName, request.ContentType, request.SizeBytes, currentUser.Id), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>Confirms the client finished uploading; publishes FileUploaded.</summary>
    /// <param name="id">File id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CompleteUploadCommand(id, currentUser.Id), ct);
        return NoContent();
    }

    /// <summary>Gets a presigned download URL for a clean file.</summary>
    /// <param name="id">File id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the download URL.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DownloadUrlDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetDownloadUrlQuery(id, currentUser.Id), ct);
        return Ok(dto);
    }
}
