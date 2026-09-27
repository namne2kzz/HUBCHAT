using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.UnitTests;

/// <summary>
/// Covers the ownership and membership invariants on <c>Channel</c>: who may leave, who may hand over,
/// and the rule that a channel is never left without an owner.
/// </summary>
/// <remarks>
/// These are the rules that decide whether a channel can become unadministrable. A channel whose last
/// owner walked out cannot have its members managed or its settings changed by anybody, and nothing in
/// the API would report that as an error — it simply stops being possible. The guard lives in the
/// domain, so this is where it is pinned down.
/// </remarks>
public sealed class ChannelOwnershipTests
{
    private static Channel NewChannel(Guid creator) =>
        Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, creator);

    // ── Removing members ────────────────────────────────────────────────────

    [Fact]
    public void TheOnlyOwnerCannotLeave()
    {
        var owner   = Guid.NewGuid();
        var channel = NewChannel(owner);
        channel.AddMember(Guid.NewGuid());

        // Leaving here would strand the channel: members remain but nobody can administer them.
        Should.Throw<DomainException>(() => channel.RemoveMember(owner))
            .Message.ShouldContain("only owner");
    }

    [Fact]
    public void AnOwnerMayLeaveOnceAnotherOwnerExists()
    {
        var first  = Guid.NewGuid();
        var second = Guid.NewGuid();

        var channel = NewChannel(first);
        channel.AddMember(second, ChannelMemberRole.Owner);

        channel.RemoveMember(first);

        channel.HasMember(first).ShouldBeFalse();
        channel.Members.ShouldHaveSingleItem().UserId.ShouldBe(second);
    }

    [Fact]
    public void AnOrdinaryMemberMayLeaveWhileTheOwnerRemains()
    {
        var owner  = Guid.NewGuid();
        var member = Guid.NewGuid();

        var channel = NewChannel(owner);
        channel.AddMember(member);

        channel.RemoveMember(member);

        channel.Members.ShouldHaveSingleItem().UserId.ShouldBe(owner);
    }

    [Fact]
    public void AnAdminIsNotAnOwnerForTheLastOwnerRule()
    {
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();

        var channel = NewChannel(owner);
        channel.AddMember(admin, ChannelMemberRole.Admin);

        // An Admin can manage members but is not a substitute owner, so the sole owner still cannot
        // leave. Counting admins toward the guard would let the channel lose its last owner.
        Should.Throw<DomainException>(() => channel.RemoveMember(owner));
    }

    [Fact]
    public void RemovingSomebodyWhoIsNotAMemberFails()
    {
        var channel = NewChannel(Guid.NewGuid());

        Should.Throw<DomainException>(() => channel.RemoveMember(Guid.NewGuid()))
            .Message.ShouldContain("not a member");
    }

    [Fact]
    public void RemovingTheSameMemberTwiceFailsTheSecondTime()
    {
        var owner  = Guid.NewGuid();
        var member = Guid.NewGuid();

        var channel = NewChannel(owner);
        channel.AddMember(member);
        channel.RemoveMember(member);

        // Not idempotent, deliberately: a second removal means the caller's view of the membership is
        // stale, which the application layer should surface rather than swallow.
        Should.Throw<DomainException>(() => channel.RemoveMember(member));
    }

    // ── Transferring ownership ──────────────────────────────────────────────

    [Fact]
    public void TransferringOwnershipPromotesTheTargetAndDemotesTheFormerOwnerToAdmin()
    {
        var owner    = Guid.NewGuid();
        var newOwner = Guid.NewGuid();

        var channel = NewChannel(owner);
        channel.AddMember(newOwner);

        channel.TransferOwnership(owner, newOwner);

        channel.Members.Single(m => m.UserId == newOwner).Role.ShouldBe(ChannelMemberRole.Owner);

        // Demoted rather than removed: the previous owner keeps administrative access, which is what
        // makes handing over safe to do without a second person present.
        channel.Members.Single(m => m.UserId == owner).Role.ShouldBe(ChannelMemberRole.Admin);
    }

    [Fact]
    public void TransferringToYourselfFails()
    {
        var owner   = Guid.NewGuid();
        var channel = NewChannel(owner);

        // Allowed through, this would demote the owner to Admin and leave the channel ownerless.
        Should.Throw<DomainException>(() => channel.TransferOwnership(owner, owner))
            .Message.ShouldContain("already own");
    }

    [Fact]
    public void OnlyTheCurrentOwnerMayTransferOwnership()
    {
        var owner  = Guid.NewGuid();
        var admin  = Guid.NewGuid();
        var member = Guid.NewGuid();

        var channel = NewChannel(owner);
        channel.AddMember(admin, ChannelMemberRole.Admin);
        channel.AddMember(member);

        // An Admin can manage members but cannot appoint itself owner.
        Should.Throw<DomainException>(() => channel.TransferOwnership(admin, member));
    }

    [Fact]
    public void ANonMemberCannotTransferOwnership()
    {
        var owner   = Guid.NewGuid();
        var channel = NewChannel(owner);
        channel.AddMember(Guid.NewGuid());

        Should.Throw<DomainException>(() => channel.TransferOwnership(Guid.NewGuid(), owner));
    }

    [Fact]
    public void OwnershipCannotBeTransferredToANonMember()
    {
        var owner   = Guid.NewGuid();
        var channel = NewChannel(owner);

        // Otherwise the channel's owner would be somebody with no membership row at all.
        Should.Throw<DomainException>(() => channel.TransferOwnership(owner, Guid.NewGuid()))
            .Message.ShouldContain("not a member");
    }

    [Fact]
    public void AFailedTransferLeavesRolesUntouched()
    {
        var owner  = Guid.NewGuid();
        var member = Guid.NewGuid();

        var channel = NewChannel(owner);
        channel.AddMember(member);

        Should.Throw<DomainException>(() => channel.TransferOwnership(owner, Guid.NewGuid()));

        // The method demotes the current owner as part of its work, so a failure part-way through
        // must not leave the channel with two owners or none.
        channel.Members.Single(m => m.UserId == owner).Role.ShouldBe(ChannelMemberRole.Owner);
        channel.Members.Single(m => m.UserId == member).Role.ShouldBe(ChannelMemberRole.Member);
    }

    [Fact]
    public void AfterTransferringTheNewOwnerCanTransferOnward()
    {
        var first  = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third  = Guid.NewGuid();

        var channel = NewChannel(first);
        channel.AddMember(second);
        channel.AddMember(third);

        channel.TransferOwnership(first, second);
        channel.TransferOwnership(second, third);

        channel.Members.Single(m => m.UserId == third).Role.ShouldBe(ChannelMemberRole.Owner);
        channel.Members.Count(m => m.Role == ChannelMemberRole.Owner)
            .ShouldBe(1, "a channel has exactly one owner at a time");
    }

    [Fact]
    public void AfterTransferringTheFormerOwnerMayLeave()
    {
        var first  = Guid.NewGuid();
        var second = Guid.NewGuid();

        var channel = NewChannel(first);
        channel.AddMember(second);
        channel.TransferOwnership(first, second);

        // The point of handing over: it is what unblocks leaving.
        channel.RemoveMember(first);

        channel.HasMember(first).ShouldBeFalse();
    }
}
