using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for adding a user to a channel by the calling admin/owner.</summary>
/// <param name="UserId">User to add.</param>
public sealed record AddChannelMemberRequest(Guid UserId);
