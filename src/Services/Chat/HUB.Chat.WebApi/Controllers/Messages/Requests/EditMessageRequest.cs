namespace HUB.Chat.WebApi.Controllers.Messages.Requests;

/// <summary>Body for editing a message.</summary>
/// <param name="Body">The new message body.</param>
public sealed record EditMessageRequest(string Body);
