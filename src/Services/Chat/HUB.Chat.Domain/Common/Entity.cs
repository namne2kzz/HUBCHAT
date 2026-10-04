namespace HUB.Chat.Domain.Common;

/// <summary>Base class for all domain entities: identity + audit timestamps.</summary>
public abstract class Entity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>UTC creation time.</summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    /// <summary>UTC time of last update; null if never updated.</summary>
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>Stamps <see cref="UpdatedAt"/> with the current UTC time.</summary>
    protected void Touch() => UpdatedAt = DateTime.UtcNow;
}
