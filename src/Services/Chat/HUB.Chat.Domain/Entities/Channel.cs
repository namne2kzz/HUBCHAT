using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Domain.Entities;

/// <summary>A conversation space (public/private channel, DM or group DM). Aggregate root over its members.</summary>
public sealed class Channel : AggregateRoot
{
    private readonly List<ChannelMember> _members = [];

    private Channel() { } // EF

    private Channel(Guid workspaceId, string name, ChannelType type, Guid createdBy, string topic)
    {
        WorkspaceId = workspaceId;
        Name        = name;
        Slug        = Slugify(name);
        Type        = type;
        Topic       = topic;
        IsPrivate   = type is ChannelType.Private or ChannelType.Dm or ChannelType.GroupDm;
        CreatedBy   = createdBy;
    }

    /// <summary>Workspace (= DASHBOARD Repository/Project) this channel belongs to.</summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>Display name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>URL-friendly slug derived from the name.</summary>
    public string Slug { get; private set; } = default!;

    /// <summary>Channel kind.</summary>
    public ChannelType Type { get; private set; }

    /// <summary>Optional topic/description.</summary>
    public string Topic { get; private set; } = string.Empty;

    /// <summary>Whether the channel is invite-only.</summary>
    public bool IsPrivate { get; private set; }

    /// <summary>User who created the channel.</summary>
    public Guid CreatedBy { get; private set; }

    /// <summary>Whether the channel is archived (hidden, read-only).</summary>
    public bool IsArchived { get; private set; }

    /// <summary>Type of linked DASHBOARD resource, if this channel is a discussion thread for one.</summary>
    public LinkedResourceType? LinkType { get; private set; }

    /// <summary>Linked resource id in DASHBOARD (e.g. work item id).</summary>
    public Guid? LinkExternalId { get; private set; }

    /// <summary>Human-readable key of the linked resource (e.g. "DASH-142").</summary>
    public string? LinkExternalKey { get; private set; }

    /// <summary>Deep-link URL back to the resource in DASHBOARD.</summary>
    public string LinkUrl { get; private set; } = string.Empty;

    /// <summary>True when this channel is a discussion thread linked to a DASHBOARD resource.</summary>
    public bool IsLinked => LinkExternalId is not null;

    /// <summary>The channel's members.</summary>
    public IReadOnlyCollection<ChannelMember> Members => _members.AsReadOnly();

    /// <summary>Creates a new channel and adds the creator as owner.</summary>
    /// <param name="workspaceId">Owning workspace.</param>
    /// <param name="name">Display name (required).</param>
    /// <param name="type">Channel kind.</param>
    /// <param name="createdBy">Creator user id.</param>
    /// <param name="topic">Optional topic.</param>
    /// <returns>The new channel.</returns>
    public static Channel Create(Guid workspaceId, string name, ChannelType type, Guid createdBy, string topic = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Channel name is required.");

        var channel = new Channel(workspaceId, name.Trim(), type, createdBy, topic?.Trim() ?? string.Empty);
        channel.AddMember(createdBy, ChannelMemberRole.Owner);
        return channel;
    }

    /// <summary>Adds a member if not already present.</summary>
    /// <param name="userId">User to add.</param>
    /// <param name="role">Role to grant.</param>
    /// <returns>The created membership.</returns>
    public ChannelMember AddMember(Guid userId, ChannelMemberRole role = ChannelMemberRole.Member)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new DomainException("User is already a member of this channel.");

        var member = new ChannelMember(Id, userId, role);
        _members.Add(member);
        // Note: Touch() is intentionally omitted here.
        // Member changes do not update the channel definition; touching the aggregate
        // would trigger an EF UPDATE on the Channel with its RowVersion concurrency token,
        // causing DbUpdateConcurrencyException when the DB-generated value has changed.
        return member;
    }

    /// <summary>Returns true when the given user is a member.</summary>
    /// <param name="userId">User id.</param>
    public bool HasMember(Guid userId) => _members.Any(m => m.UserId == userId);

    /// <summary>Ensures the channel can accept new messages.</summary>
    public void EnsureWritable()
    {
        if (IsArchived) throw new DomainException("Channel is archived.");
    }

    /// <summary>Removes a member from the channel. The last owner cannot leave without transferring ownership.</summary>
    /// <param name="userId">User to remove.</param>
    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("User is not a member of this channel.");

        var ownerCount = _members.Count(m => m.Role == ChannelMemberRole.Owner);
        if (member.Role == ChannelMemberRole.Owner && ownerCount == 1)
            throw new DomainException("You are the only owner — transfer ownership before leaving.");

        _members.Remove(member);
        // Note: Touch() is intentionally omitted — see AddMember for reasoning.
    }

    /// <summary>Updates the channel name and/or topic. Only non-empty values are applied.</summary>
    /// <param name="name">New display name (null/empty = keep current).</param>
    /// <param name="topic">New topic (null = keep current).</param>
    public void UpdateInfo(string? name, string? topic)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
            Slug = Slugify(Name);
        }
        if (topic is not null)
            Topic = topic.Trim();
        Touch();
    }

    /// <summary>Switches a regular channel between Public and Private. Direct messages cannot change visibility.</summary>
    /// <param name="isPrivate">True to make the channel private; false to make it public.</param>
    public void ChangeVisibility(bool isPrivate)
    {
        if (Type is ChannelType.Dm or ChannelType.GroupDm)
            throw new DomainException("Direct messages cannot change visibility.");
        Type      = isPrivate ? ChannelType.Private : ChannelType.Public;
        IsPrivate = isPrivate;
        Touch();
    }

    /// <summary>Transfers ownership to another member: the new owner becomes Owner, the previous owner is demoted to Admin.</summary>
    /// <param name="currentOwnerId">The acting current owner.</param>
    /// <param name="newOwnerId">The member to promote to Owner.</param>
    public void TransferOwnership(Guid currentOwnerId, Guid newOwnerId)
    {
        if (currentOwnerId == newOwnerId)
            throw new DomainException("You already own this channel.");

        var current = _members.FirstOrDefault(m => m.UserId == currentOwnerId && m.Role == ChannelMemberRole.Owner)
            ?? throw new DomainException("Only the current owner can transfer ownership.");
        var next = _members.FirstOrDefault(m => m.UserId == newOwnerId)
            ?? throw new DomainException("The target user is not a member of this channel.");

        next.ChangeRole(ChannelMemberRole.Owner);
        current.ChangeRole(ChannelMemberRole.Admin);
        Touch();
    }

    /// <summary>Archives the channel.</summary>
    public void Archive()
    {
        IsArchived = true;
        Touch();
    }

    /// <summary>Links this channel to a DASHBOARD resource (idempotent for the same resource).</summary>
    /// <param name="type">Resource kind.</param>
    /// <param name="externalId">Resource id in DASHBOARD.</param>
    /// <param name="externalKey">Human-readable key (e.g. "DASH-142").</param>
    /// <param name="url">Deep-link URL back to DASHBOARD.</param>
    public void LinkTo(LinkedResourceType type, Guid externalId, string externalKey, string url)
    {
        if (externalId == Guid.Empty) throw new DomainException("Linked resource id is required.");
        LinkType        = type;
        LinkExternalId  = externalId;
        LinkExternalKey = externalKey?.Trim() ?? string.Empty;
        LinkUrl         = url?.Trim() ?? string.Empty;
        Touch();
    }

    private static string Slugify(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
