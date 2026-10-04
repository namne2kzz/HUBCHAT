using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Domain.Entities;

/// <summary>A message posted in a channel (top-level or thread reply). Aggregate root over its reactions.</summary>
public sealed class Message : AggregateRoot
{
    private readonly List<Reaction> _reactions = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<Guid> _mentions = [];

    private Message() { } // EF

    private Message(Guid channelId, Guid authorId, string body, MessageFormat format, Guid? parentId, Guid? replyToId, IEnumerable<Guid> mentions)
    {
        ChannelId = channelId;
        AuthorId  = authorId;
        Body      = body;
        Format    = format;
        ParentId  = parentId;
        ReplyToId = replyToId;
        _mentions.AddRange(mentions.Distinct());
    }

    /// <summary>Channel this message belongs to.</summary>
    public Guid ChannelId { get; private set; }

    /// <summary>Parent message id when this is a thread reply; null for top-level messages.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>Message id this one quote-replies to (distinct from a thread parent); null otherwise.</summary>
    public Guid? ReplyToId { get; private set; }

    /// <summary>Original message id when this message was forwarded; null otherwise.</summary>
    public Guid? ForwardedFromId { get; private set; }

    /// <summary>Author user id.</summary>
    public Guid AuthorId { get; private set; }

    /// <summary>Message body.</summary>
    public string Body { get; private set; } = default!;

    /// <summary>Body format.</summary>
    public MessageFormat Format { get; private set; }

    /// <summary>User ids explicitly @mentioned in the body.</summary>
    public IReadOnlyList<Guid> Mentions => _mentions.AsReadOnly();

    /// <summary>
    /// Client-generated idempotency key for the send that created this message; null for clients that do
    /// not send one. Unique per author, so a retried send resolves to this message instead of a duplicate.
    /// </summary>
    public Guid? ClientMessageId { get; private set; }

    /// <summary>UTC time the message was last edited; null if never edited.</summary>
    public DateTime? EditedAt { get; private set; }

    /// <summary>UTC time the message was soft-deleted; null if not deleted.</summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>True when the message has been soft-deleted.</summary>
    public bool IsDeleted => DeletedAt is not null;

    /// <summary>Reactions on this message.</summary>
    public IReadOnlyCollection<Reaction> Reactions => _reactions.AsReadOnly();

    /// <summary>Files/images/videos attached to this message.</summary>
    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    /// <summary>Creates a new message.</summary>
    /// <param name="channelId">Target channel.</param>
    /// <param name="authorId">Author user id.</param>
    /// <param name="body">Message body (required).</param>
    /// <param name="format">Body format.</param>
    /// <param name="parentId">Parent message id for a thread reply; null otherwise.</param>
    /// <param name="replyToId">Message id this quote-replies to; null otherwise.</param>
    /// <param name="mentions">User ids mentioned in the body.</param>
    /// <param name="clientMessageId">Client idempotency key for this send; null when the client sends none.</param>
    /// <returns>The new message.</returns>
    public static Message Post(
        Guid channelId, Guid authorId, string body, MessageFormat format = MessageFormat.Markdown,
        Guid? parentId = null, Guid? replyToId = null, IEnumerable<Guid>? mentions = null,
        Guid? clientMessageId = null)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new DomainException("Message body cannot be empty.");
        if (clientMessageId == Guid.Empty)
            throw new DomainException("Client message id cannot be empty.");

        return new Message(channelId, authorId, body.Trim(), format, parentId, replyToId, mentions ?? [])
        {
            ClientMessageId = clientMessageId,
        };
    }

    /// <summary>Attaches a stored file/image/video to this message.</summary>
    /// <param name="kind">Attachment kind.</param>
    /// <param name="url">Storage key/URL in MinIO.</param>
    /// <param name="name">Original file name.</param>
    /// <param name="size">Size in bytes.</param>
    /// <param name="mime">MIME type.</param>
    /// <param name="width">Pixel width (media); null otherwise.</param>
    /// <param name="height">Pixel height (media); null otherwise.</param>
    public void AddAttachment(AttachmentKind kind, string url, string name, long size, string mime, int? width = null, int? height = null)
        => _attachments.Add(new Attachment(Id, kind, url, name, size, mime, width, height));

    /// <summary>Edits the body. Only the author may edit (enforced in the application layer).</summary>
    /// <param name="newBody">The new body (required).</param>
    public void Edit(string newBody)
    {
        if (IsDeleted) throw new DomainException("Cannot edit a deleted message.");
        if (string.IsNullOrWhiteSpace(newBody)) throw new DomainException("Message body cannot be empty.");
        Body     = newBody.Trim();
        EditedAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>Soft-deletes the message.</summary>
    public void SoftDelete()
    {
        if (IsDeleted) return;
        DeletedAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>Adds a reaction if the user has not already reacted with the same emoji.</summary>
    /// <param name="userId">Reacting user.</param>
    /// <param name="emoji">Emoji shortcode.</param>
    /// <returns>True when the reaction was added; false when the user already had it (no change).</returns>
    public bool AddReaction(Guid userId, string emoji)
    {
        if (IsDeleted) throw new DomainException("Cannot react to a deleted message.");
        if (string.IsNullOrWhiteSpace(emoji)) throw new DomainException("Emoji is required.");
        if (_reactions.Any(r => r.UserId == userId && r.Emoji == emoji)) return false;
        _reactions.Add(new Reaction(Id, userId, emoji));
        Touch();
        return true;
    }

    /// <summary>Removes a user's reaction if present.</summary>
    /// <param name="userId">Reacting user.</param>
    /// <param name="emoji">Emoji shortcode.</param>
    public void RemoveReaction(Guid userId, string emoji)
    {
        var existing = _reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji);
        if (existing is not null) { _reactions.Remove(existing); Touch(); }
    }
}
