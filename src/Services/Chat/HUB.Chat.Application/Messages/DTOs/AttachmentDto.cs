using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Messages.DTOs;

/// <summary>A message attachment (file/image/video).</summary>
public sealed record AttachmentDto(
    Guid Id, AttachmentKind Kind, string Url, string Name, long Size, string Mime, int? Width, int? Height);
