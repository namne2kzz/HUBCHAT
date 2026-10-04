using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for creating a channel.</summary>
/// <param name="WorkspaceId">Owning workspace (= DASHBOARD repository).</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Channel kind.</param>
/// <param name="Topic">Optional topic.</param>
public sealed record CreateChannelRequest(Guid WorkspaceId, string Name, ChannelType Type, string? Topic);
