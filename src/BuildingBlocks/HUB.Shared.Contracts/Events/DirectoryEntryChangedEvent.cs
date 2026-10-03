// Namespace and shape must stay byte-identical to DASHBOARD's copy
// (DASHBOARD/Application/Contracts/DirectoryEntryChangedEvent.cs): MassTransit routes by message type
// name, so renaming or moving either copy silently stops delivery. Enum values are wire format too —
// only append new ones, never renumber.
namespace Shared.IntegrationEvents;

/// <summary>Which kind of directory entry a <see cref="DirectoryEntryChangedEvent"/> invalidates.</summary>
public enum DirectoryEntryKind
{
    /// <summary>A user's profile — display name, avatar, admin flag or active state.</summary>
    UserProfile = 0,

    /// <summary>A user's display-preference settings.</summary>
    UserSettings = 1,

    /// <summary>A work item's linkable context — key, title or state.</summary>
    WorkItem = 2,
}

/// <summary>
/// Raised by DASHBOARD when something the gateway caches has changed. Consumed here to evict that one
/// entry instead of serving it until its TTL expires.
/// </summary>
/// <param name="Kind">Which cached entry went stale.</param>
/// <param name="EntityId">Id of the user or work item the entry belongs to.</param>
public sealed record DirectoryEntryChangedEvent(DirectoryEntryKind Kind, Guid EntityId);
