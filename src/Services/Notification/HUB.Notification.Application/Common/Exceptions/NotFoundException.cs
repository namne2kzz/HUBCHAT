namespace HUB.Notification.Application.Common.Exceptions;

/// <summary>Thrown when a referenced entity does not exist. Mapped to HTTP 404.</summary>
/// <param name="message">Reason.</param>
public sealed class NotFoundException(string message = "Resource not found.") : Exception(message);
