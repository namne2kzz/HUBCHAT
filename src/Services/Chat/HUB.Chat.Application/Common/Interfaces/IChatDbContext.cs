using HUB.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Common.Interfaces;

/// <summary>Abstraction over the chat persistence context used by application handlers.</summary>
public interface IChatDbContext
{
    /// <summary>Channels with their members.</summary>
    DbSet<Channel> Channels { get; }

    /// <summary>Messages with their reactions.</summary>
    DbSet<Message> Messages { get; }

    /// <summary>Persists pending changes.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Stops tracking every pending change (including outbox rows) so a later save does not retry them.
    /// Use after a failed save — e.g. a lost find-or-create race — before re-reading and saving again.
    /// </summary>
    void DiscardChanges();
}
