using HUB.Chat.Domain.Common;

namespace HUB.Chat.Domain.Entities;

/// <summary>An emoji reaction to a <see cref="Message"/> (part of the Message aggregate).</summary>
public sealed class Reaction : Entity
{
    private Reaction() { } // EF

    internal Reaction(Guid messageId, Guid userId, string emoji)
    {
        MessageId = messageId;
        UserId    = userId;
        Emoji     = emoji;
    }

    /// <summary>Owning message id.</summary>
    public Guid MessageId { get; private set; }

    /// <summary>User who reacted.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Emoji shortcode (e.g. ":thumbsup:").</summary>
    public string Emoji { get; private set; } = default!;
}
