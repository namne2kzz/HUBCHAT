using HUB.Media.Application.Files.DTOs;
using MediatR;

namespace HUB.Media.Application.Files.Queries.GetDownloadUrl;

/// <summary>Gets a presigned GET URL for a downloadable (clean) file.</summary>
public sealed record GetDownloadUrlQuery(Guid FileId, Guid ActingUserId) : IRequest<DownloadUrlDto>;
