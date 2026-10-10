namespace HUB.Notification.Application.Common.Exceptions;

/// <summary>
/// Thrown by <c>SaveChangesAsync</c> when a write hits a unique index (PostgreSQL 23505, translated in
/// Infrastructure so this layer stays provider-agnostic).
/// </summary>
/// <param name="constraintName">Name of the violated index, when reported.</param>
/// <param name="innerException">The original provider exception.</param>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName ?? "unknown"}' was violated.", innerException)
{
    /// <summary>Name of the violated index, or null when the provider does not report it.</summary>
    public string? ConstraintName { get; } = constraintName;
}
