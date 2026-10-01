using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Channels.DTOs;

/// <summary>Mapping helpers from the <see cref="Channel"/> aggregate to <see cref="ChannelDto"/>.</summary>
public static class ChannelMappings
{
    /// <summary>
    /// Projects a loaded channel (with its members) to its DTO, resolving membership for the acting user.
    /// </summary>
    /// <param name="c">The channel aggregate; its <c>Members</c> must be loaded.</param>
    /// <param name="actingUserId">The user the DTO is being built for — decides <c>IsMember</c> and <c>MyRole</c>.</param>
    /// <param name="otherUserId">For a DM: the other participant, when the caller already knows it.</param>
    /// <returns>The channel DTO.</returns>
    /// <remarks>
    /// The acting user is a required argument rather than an optional role parameter. It used to be
    /// <c>myRole = null</c>, which every caller was free to forget — and five of them did, so a channel
    /// created or updated came back saying <c>MyRole: null</c> while fetching the same channel said
    /// <c>Owner</c>. Deriving it here means a caller cannot silently omit it.
    /// </remarks>
    public static ChannelDto ToDto(this Channel c, Guid actingUserId, Guid? otherUserId = null)
    {
        var member = c.Members.FirstOrDefault(m => m.UserId == actingUserId);

        return new ChannelDto(
            c.Id, c.WorkspaceId, c.Name, c.Slug, c.Type, c.Topic, c.IsPrivate, c.IsArchived,
            c.Members.Count, c.CreatedAt, c.LinkType, c.LinkExternalKey, c.LinkUrl,
            IsMember: member is not null,
            OtherUserId: otherUserId ?? OtherParticipant(c, actingUserId),
            MyRole: member?.Role);
    }

    /// <summary>For a DM or group DM, the first member who is not the acting user; null for other channels.</summary>
    private static Guid? OtherParticipant(Channel c, Guid actingUserId) =>
        c.Type is ChannelType.Dm or ChannelType.GroupDm
            ? c.Members.FirstOrDefault(m => m.UserId != actingUserId)?.UserId
            : null;

    /// <summary>Projects a <see cref="ChannelMember"/> to its DTO.</summary>
    /// <param name="m">The membership.</param>
    /// <returns>The member DTO.</returns>
    public static ChannelMemberDto ToMemberDto(this ChannelMember m) =>
        new(m.UserId, m.Role, m.Muted, m.JoinedAt);
}
