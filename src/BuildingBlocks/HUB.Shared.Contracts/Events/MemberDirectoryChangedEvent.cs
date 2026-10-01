// Namespace and record shape must stay byte-identical to DASHBOARD's copy
// (DASHBOARD/Application/Contracts/MemberDirectoryChangedEvent.cs): MassTransit routes by message type
// name, so renaming or moving either copy silently stops delivery. The namespace is neutral rather
// than HUB.Shared.Contracts.Events precisely because neither system owns this contract alone.
namespace Shared.IntegrationEvents;

/// <summary>
/// Raised by DASHBOARD whenever a repository's membership changes. Consumed here so the gateway drops
/// its cached directory entries immediately instead of serving them until their TTL expires.
/// </summary>
/// <remarks>
/// Does not derive from <c>IntegrationEvent</c>: that base lives in HUB and DASHBOARD cannot reference
/// it, and the two copies have to match exactly.
/// </remarks>
/// <param name="RepositoryId">The repository whose membership changed.</param>
/// <param name="UserId">The affected user, whose cached memberships must also be dropped.</param>
public sealed record MemberDirectoryChangedEvent(Guid RepositoryId, Guid UserId);
