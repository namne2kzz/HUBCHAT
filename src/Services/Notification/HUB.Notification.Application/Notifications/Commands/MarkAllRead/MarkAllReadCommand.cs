using MediatR;

namespace HUB.Notification.Application.Notifications.Commands.MarkAllRead;

/// <summary>Marks all of the acting user's notifications read.</summary>
public sealed record MarkAllReadCommand(Guid ActingUserId) : IRequest;
