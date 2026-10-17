namespace HUB.Chat.WebApi.Controllers.Messages.Requests;

/// <summary>Body for forwarding a message to another channel.</summary>
/// <param name="TargetChannelId">The channel to forward the message into.</param>
public sealed record ForwardMessageRequest(Guid TargetChannelId);
