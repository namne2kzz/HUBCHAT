namespace HUB.Chat.Application.Common.Exceptions;

/// <summary>
/// Thrown by <c>SaveChangesAsync</c> when an insert/update hits a unique index. Mapped to HTTP 409 if a
/// handler does not handle it.
/// </summary>
/// <remarks>
/// Lets find-or-create and idempotent handlers react to "someone else inserted it first" without the
/// Application layer knowing which database provider raised it — Infrastructure translates the
/// provider error (PostgreSQL <c>23505</c>) into this type.
/// </remarks>
/// <param name="constraintName">Name of the violated index/constraint, when the provider reports it.</param>
/// <param name="innerException">The original provider exception.</param>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName ?? "unknown"}' was violated.", innerException)
{
    /// <summary>Name of the violated index/constraint, or null when the provider does not report it.</summary>
    public string? ConstraintName { get; } = constraintName;
}
