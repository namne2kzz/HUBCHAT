using MediatR;

namespace HUB.Chat.Application.Channels.Commands.MarkRead;

/// <summary>Marks a channel read up to a point in time for the acting user.</summary>
/// <param name="ChannelId">Channel to mark.</param>
/// <param name="ActingUserId">The user.</param>
/// <param name="ReadAt">Point in time read up to; defaults to now when null.</param>
public sealed record MarkReadCommand(Guid ChannelId, Guid ActingUserId, DateTime? ReadAt) : IRequest;
