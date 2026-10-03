using HUB.DashboardGateway.Directory;
using MassTransit;
using Shared.IntegrationEvents;

namespace HUB.DashboardGateway.Consumers;

/// <summary>
/// Consumes <see cref="DirectoryEntryChangedEvent"/> from DASHBOARD and evicts the one cached entry it
/// names, so a profile, settings or work-item change shows up here immediately rather than after the
/// TTL.
/// </summary>
/// <remarks>
/// Idempotent by construction: evicting an absent key is a no-op, so MassTransit's at-least-once
/// delivery and retries need no deduplication. If the event is lost entirely the TTL still expires the
/// entry, which is exactly the behaviour that existed before this consumer.
/// </remarks>
/// <param name="directory">Directory cache whose entries are evicted.</param>
/// <param name="logger">Logger, so a lost invalidation is traceable when stale data is reported.</param>
public sealed class DirectoryEntryChangedConsumer(
    IDirectoryService directory,
    ILogger<DirectoryEntryChangedConsumer> logger) : IConsumer<DirectoryEntryChangedEvent>
{
    /// <summary>Evicts the cached entry matching the event's kind and id.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes once the entry has been evicted.</returns>
    public async Task Consume(ConsumeContext<DirectoryEntryChangedEvent> context)
    {
        var (kind, entityId) = context.Message;
        var ct = context.CancellationToken;

        switch (kind)
        {
            case DirectoryEntryKind.UserProfile:
                await directory.InvalidateUserProfileAsync(entityId, ct);
                break;

            case DirectoryEntryKind.UserSettings:
                await directory.InvalidateUserSettingsAsync(entityId, ct);
                break;

            case DirectoryEntryKind.WorkItem:
                await directory.InvalidateWorkItemAsync(entityId, ct);
                break;

            default:
                // A kind added on DASHBOARD's side but not yet handled here. Log and move on rather
                // than throw: retrying cannot help, and dead-lettering it would hide the mismatch.
                logger.LogWarning(
                    "Unhandled directory entry kind {Kind} for entity {EntityId}; cached entry left to expire by TTL.",
                    kind, entityId);
                return;
        }

        logger.LogInformation("Invalidated {Kind} directory cache for {EntityId}.", kind, entityId);
    }
}
