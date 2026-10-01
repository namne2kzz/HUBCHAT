using HUB.DashboardGateway.Directory;
using MassTransit;
using Shared.IntegrationEvents;

namespace HUB.DashboardGateway.Consumers;

/// <summary>
/// Consumes <see cref="MemberDirectoryChangedEvent"/> from DASHBOARD and evicts the directory entries
/// it invalidated, so a membership change shows up here immediately instead of after the TTL.
/// </summary>
/// <remarks>
/// Idempotent by construction: evicting an already-absent key is a no-op, so MassTransit's retries and
/// at-least-once delivery need no deduplication. Worst case the next read costs one extra pull from
/// DASHBOARD; if the event is lost entirely, the TTL still expires the entry as before.
/// </remarks>
/// <param name="directory">Directory cache whose entries are evicted.</param>
/// <param name="logger">Logger, so a lost invalidation is traceable when stale data is reported.</param>
public sealed class MemberDirectoryChangedConsumer(
    IDirectoryService directory,
    ILogger<MemberDirectoryChangedConsumer> logger) : IConsumer<MemberDirectoryChangedEvent>
{
    /// <summary>Evicts the repository's member list and the affected user's memberships.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes once the entries have been evicted.</returns>
    public async Task Consume(ConsumeContext<MemberDirectoryChangedEvent> context)
    {
        var msg = context.Message;

        await directory.InvalidateMembershipAsync(msg.RepositoryId, msg.UserId, context.CancellationToken);

        logger.LogInformation(
            "Invalidated directory cache for repository {RepositoryId}, user {UserId}.",
            msg.RepositoryId, msg.UserId);
    }
}
