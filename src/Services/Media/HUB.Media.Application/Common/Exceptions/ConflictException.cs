namespace HUB.Media.Application.Common.Exceptions;

/// <summary>Operation not allowed in the file's current state. Mapped to HTTP 409.</summary>
/// <param name="message">Reason.</param>
public sealed class ConflictException(string message) : Exception(message);
