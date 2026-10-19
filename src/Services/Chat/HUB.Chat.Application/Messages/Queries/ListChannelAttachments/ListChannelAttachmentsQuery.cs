using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Enums;
using MediatR;

namespace HUB.Chat.Application.Messages.Queries.ListChannelAttachments;

/// <summary>
/// Lists attachments shared in a channel for the Files/Media panel (CF-5), newest first.
/// </summary>
/// <param name="ChannelId">Channel to read.</param>
/// <param name="ActingUserId">Requesting user (must be able to read the channel).</param>
/// <param name="Kind">Optional filter: only this kind (Image/Video/File); null = all.</param>
/// <param name="Limit">Max rows to return (1..100).</param>
public sealed record ListChannelAttachmentsQuery(
    Guid ChannelId,
    Guid ActingUserId,
    AttachmentKind? Kind = null,
    int Limit = 60) : IRequest<IReadOnlyList<ChannelAttachmentDto>>;
