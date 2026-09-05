using MediatR;

namespace HUB.Chat.Application.Channels.Commands.ArchiveChannel;

/// <summary>
/// Archives a channel, making it read-only. Used when a linked sprint is closed.
/// Idempotent — no-ops if already archived.
/// </summary>
/// <param name="ChannelId">Channel to archive.</param>
public sealed record ArchiveChannelCommand(Guid ChannelId) : IRequest;
