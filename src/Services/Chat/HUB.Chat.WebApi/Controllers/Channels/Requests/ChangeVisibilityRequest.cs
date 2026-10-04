using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for switching a channel between Public and Private (owner only).</summary>
/// <param name="IsPrivate">True to make the channel private; false for public.</param>
public sealed record ChangeVisibilityRequest(bool IsPrivate);
