namespace HUB.Chat.Domain.Common;

/// <summary>Thrown when a domain invariant is violated. Mapped to HTTP 409/422 at the API boundary.</summary>
/// <param name="message">Human-readable invariant description.</param>
public sealed class DomainException(string message) : Exception(message);
