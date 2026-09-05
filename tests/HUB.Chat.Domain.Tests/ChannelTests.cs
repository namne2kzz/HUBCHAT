using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.Tests;

public sealed class ChannelTests
{
    [Fact]
    public void Create_AddsCreatorAsOwner()
    {
        var creator = Guid.NewGuid();

        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, creator);

        channel.Members.ShouldHaveSingleItem();
        channel.Members.Single().UserId.ShouldBe(creator);
        channel.Members.Single().Role.ShouldBe(ChannelMemberRole.Owner);
        channel.Slug.ShouldBe("general");
    }

    [Fact]
    public void Create_PrivateType_IsPrivate()
    {
        var channel = Channel.Create(Guid.NewGuid(), "Secret", ChannelType.Private, Guid.NewGuid());
        channel.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Should.Throw<DomainException>(() => Channel.Create(Guid.NewGuid(), "  ", ChannelType.Public, Guid.NewGuid()));
    }

    [Fact]
    public void AddMember_Duplicate_Throws()
    {
        var user = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, Guid.NewGuid());
        channel.AddMember(user);

        Should.Throw<DomainException>(() => channel.AddMember(user));
    }

    [Fact]
    public void PostToArchived_IsBlocked()
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, Guid.NewGuid());
        channel.Archive();

        Should.Throw<DomainException>(channel.EnsureWritable);
    }
}
