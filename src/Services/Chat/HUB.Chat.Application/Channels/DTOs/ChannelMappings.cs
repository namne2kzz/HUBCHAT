using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Channels.DTOs;

/// <summary>Mapping helpers from the <see cref="Channel"/> aggregate to <see cref="ChannelDto"/>.</summary>
public static class ChannelMappings
{
    /// <summary>Projects a loaded channel (with members) to its DTO. Acting user is assumed to be a member (owner/creator context).</summary>
    /// <param name="c">The channel aggregate.</param>
    /// <returns>The channel DTO.</returns>
    public static ChannelDto ToDto(this Channel c, Guid? otherUserId = null, ChannelMemberRole? myRole = null) => new(
        c.Id, c.WorkspaceId, c.Name, c.Slug, c.Type, c.Topic, c.IsPrivate, c.IsArchived,
        c.Members.Count, c.CreatedAt, c.LinkType, c.LinkExternalKey, c.LinkUrl,
        IsMember: true, OtherUserId: otherUserId, MyRole: myRole);

    /// <summary>Projects a <see cref="ChannelMember"/> to its DTO.</summary>
    /// <param name="m">The membership.</param>
    /// <returns>The member DTO.</returns>
    public static ChannelMemberDto ToMemberDto(this ChannelMember m) =>
        new(m.UserId, m.Role, m.Muted, m.JoinedAt);
}
