using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for transferring channel ownership to another member (owner only).</summary>
/// <param name="NewOwnerUserId">The member to promote to Owner.</param>
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);
