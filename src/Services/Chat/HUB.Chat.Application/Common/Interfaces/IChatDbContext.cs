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
}
