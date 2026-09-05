using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Messages.Requests;

/// <summary>Body for posting a message.</summary>
/// <param name="Body">Message body.</param>
/// <param name="Format">Body format (defaults to Markdown).</param>
/// <param name="ParentId">Parent message id for a thread reply; null for top-level.</param>
/// <param name="MentionedUserIds">User ids @mentioned.</param>
public sealed record PostMessageRequest(
    string Body,
    MessageFormat Format,
    Guid? ParentId,
    IReadOnlyList<Guid>? MentionedUserIds);

/// <summary>Body for reacting to a message.</summary>
/// <param name="Emoji">Emoji shortcode.</param>
public sealed record AddReactionRequest(string Emoji);
