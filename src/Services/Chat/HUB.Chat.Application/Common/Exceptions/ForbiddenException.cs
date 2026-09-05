namespace HUB.Chat.Application.Common.Exceptions;

/// <summary>Thrown when the acting user lacks permission for an operation. Mapped to HTTP 403.</summary>
/// <param name="message">Reason.</param>
public sealed class ForbiddenException(string message = "You do not have access to this resource.") : Exception(message);

/// <summary>Thrown when a referenced entity does not exist. Mapped to HTTP 404.</summary>
/// <param name="message">Reason.</param>
public sealed class NotFoundException(string message = "Resource not found.") : Exception(message);
