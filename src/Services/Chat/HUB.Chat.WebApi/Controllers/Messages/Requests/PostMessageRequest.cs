using HUB.Chat.Application.Messages.Commands.PostMessage;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Messages.Requests;

/// <summary>Body for posting a message.</summary>
/// <param name="Body">Message body.</param>
/// <param name="Format">Body format (defaults to Markdown).</param>
/// <param name="ParentId">Parent message id for a thread reply; null for top-level.</param>
/// <param name="MentionedUserIds">User ids @mentioned.</param>
/// <param name="ClientMessageId">Optional idempotency key (UUID) generated per send; reuse it when retrying the same send.</param>
/// <param name="ReplyToId">Quote-reply target message id; null otherwise.</param>
/// <param name="Attachments">Files already uploaded via the Media service to attach (CF-5).</param>
public sealed record PostMessageRequest(
    string Body,
    MessageFormat Format,
    Guid? ParentId,
    IReadOnlyList<Guid>? MentionedUserIds,
    Guid? ClientMessageId = null,
    Guid? ReplyToId = null,
    IReadOnlyList<AttachmentInput>? Attachments = null);
