namespace HUB.Media.Application.Common.Exceptions;

/// <summary>Referenced file does not exist. Mapped to HTTP 404.</summary>
/// <param name="message">Reason.</param>
public sealed class NotFoundException(string message = "File not found.") : Exception(message);
