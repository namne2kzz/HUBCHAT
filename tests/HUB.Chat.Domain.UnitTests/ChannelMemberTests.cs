using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.UnitTests;

/// <summary>
/// Covers <c>ChannelMember</c>: the role it carries and the read marker that unread counts are derived
/// from.
/// </summary>
/// <remarks>
/// <c>MarkRead</c> only ever moves forward. That monotonic guard is what stops an out-of-order or
/// replayed request from re-marking a channel unread — and because unread counts are computed from this
/// one timestamp, losing it means the badge silently resurrects messages the person has already seen.
/// </remarks>
public sealed class ChannelMemberTests
{
    private static readonly DateTime Noon = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Returns the creator's membership, which is the only way to obtain one from outside.</summary>
    private static ChannelMember NewMember(ChannelMemberRole role = ChannelMemberRole.Member)
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, Guid.NewGuid());

        if (role is ChannelMemberRole.Owner) return channel.Members.Single();

        return channel.AddMember(Guid.NewGuid(), role);
    }

    // ── Creation ────────────────────────────────────────────────────────────

    [Fact]
    public void ANewMemberHasNotReadAnything()
    {
        // Null rather than "now": a person joining a channel has not read its backlog, so every existing
        // message should count as unread.
        NewMember().LastReadAt.ShouldBeNull();
    }

    [Fact]
    public void ANewMemberIsNotMuted()
    {
        NewMember().Muted.ShouldBeFalse();
    }

    [Fact]
    public void JoiningIsTimestamped()
    {
        NewMember().JoinedAt.ShouldNotBe(default);
    }

    [Fact]
    public void TheCreatorJoinsAsOwner()
    {
        NewMember(ChannelMemberRole.Owner).Role.ShouldBe(ChannelMemberRole.Owner);
    }

    [Fact]
    public void AMemberAddedWithoutARoleIsAnOrdinaryMember()
    {
        NewMember().Role.ShouldBe(ChannelMemberRole.Member);
    }

    // ── Roles ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChannelMemberRole.Member)]
    [InlineData(ChannelMemberRole.Admin)]
    [InlineData(ChannelMemberRole.Owner)]
    public void AMemberCanBeAddedInAnyRole(ChannelMemberRole role)
    {
        NewMember(role).Role.ShouldBe(role);
    }

    [Fact]
    public void ARoleCanBeChangedInBothDirections()
    {
        var member = NewMember();

        member.ChangeRole(ChannelMemberRole.Admin);
        member.Role.ShouldBe(ChannelMemberRole.Admin);

        member.ChangeRole(ChannelMemberRole.Member);
        member.Role.ShouldBe(ChannelMemberRole.Member);
    }

    // ── The read marker ─────────────────────────────────────────────────────

    [Fact]
    public void TheFirstMarkReadSetsTheMarker()
    {
        var member = NewMember();

        member.MarkRead(Noon);

        member.LastReadAt.ShouldBe(Noon);
    }

    [Fact]
    public void MarkingReadFurtherForwardMovesTheMarker()
    {
        var member = NewMember();

        member.MarkRead(Noon);
        member.MarkRead(Noon.AddMinutes(5));

        member.LastReadAt.ShouldBe(Noon.AddMinutes(5));
    }

    [Fact]
    public void MarkingReadWithAnEarlierTimeIsIgnored()
    {
        var member = NewMember();
        member.MarkRead(Noon);

        member.MarkRead(Noon.AddMinutes(-5));

        // The case this guards: two tabs, or a retried request arriving late. Accepting the older value
        // would make already-read messages unread again.
        member.LastReadAt.ShouldBe(Noon);
    }

    [Fact]
    public void MarkingReadWithTheSameTimeIsAnoOp()
    {
        var member = NewMember();
        member.MarkRead(Noon);

        member.MarkRead(Noon);

        // The comparison is strictly greater-than, so an identical timestamp changes nothing — which is
        // the right answer for a duplicate request.
        member.LastReadAt.ShouldBe(Noon);
    }

    [Fact]
    public void TheMarkerNeverGoesBackwardsAcrossManyCalls()
    {
        var member = NewMember();

        // Deliberately out of order, the way concurrent clients actually deliver.
        member.MarkRead(Noon.AddMinutes(3));
        member.MarkRead(Noon.AddMinutes(1));
        member.MarkRead(Noon.AddMinutes(7));
        member.MarkRead(Noon);
        member.MarkRead(Noon.AddMinutes(5));

        member.LastReadAt.ShouldBe(Noon.AddMinutes(7), "the marker keeps the furthest point reached");
    }
}
