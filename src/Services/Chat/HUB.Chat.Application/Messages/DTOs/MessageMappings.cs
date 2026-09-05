using HUB.Chat.Domain.Entities;

namespace HUB.Chat.Application.Messages.DTOs;

/// <summary>Mapping helpers from the <see cref="Message"/> aggregate to <see cref="MessageDto"/>.</summary>
public static class MessageMappings
{
    /// <summary>Projects a loaded message (with reactions) to its DTO.</summary>
    /// <param name="m">The message aggregate.</param>
    /// <returns>The message DTO.</returns>
    public static MessageDto ToDto(this Message m) => new(
        m.Id,
        m.ChannelId,
        m.ParentId,
        m.ReplyToId,
        m.ForwardedFromId,
        m.AuthorId,
        m.Body,
        m.Format,
        m.Mentions,
        m.Reactions.Select(r => new ReactionDto(r.Emoji, r.UserId)).ToList(),
        m.Attachments.Select(a => new AttachmentDto(a.Id, a.Kind, a.Url, a.Name, a.Size, a.Mime, a.Width, a.Height)).ToList(),
        m.EditedAt,
        m.CreatedAt);
}
