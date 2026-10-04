using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for marking a channel read.</summary>
/// <param name="ReadAt">Point in time read up to; null = now.</param>
public sealed record MarkReadRequest(DateTime? ReadAt);
