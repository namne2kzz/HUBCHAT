using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for updating a channel's name and/or topic.</summary>
/// <param name="Name">New display name (null = keep current).</param>
/// <param name="Topic">New topic (null = keep current).</param>
public sealed record UpdateChannelRequest(string? Name, string? Topic);
