namespace HUB.Media.Domain.Common;

/// <summary>Base entity: identity + audit timestamps.</summary>
public abstract class Entity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>UTC creation time.</summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
}

/// <summary>Thrown when a media domain invariant is violated.</summary>
/// <param name="message">Reason.</param>
public sealed class DomainException(string message) : Exception(message);
