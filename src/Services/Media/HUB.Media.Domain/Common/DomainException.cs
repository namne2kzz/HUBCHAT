namespace HUB.Media.Domain.Common;

/// <summary>Thrown when a media domain invariant is violated.</summary>
/// <param name="message">Reason.</param>
public sealed class DomainException(string message) : Exception(message);
