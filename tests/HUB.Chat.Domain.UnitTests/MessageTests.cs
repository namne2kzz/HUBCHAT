using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.UnitTests;

public sealed class MessageTests
{
    private static Message NewMessage() =>
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hello", MessageFormat.Markdown);

    [Fact]
    public void Post_EmptyBody_Throws()
    {
        Should.Throw<DomainException>(() => Message.Post(Guid.NewGuid(), Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Post_EmptyClientMessageId_Throws()
    {
        // Guid.Empty is what a client gets from a broken UUID generator; accepting it would make every such
        // send collide on one key and silently return the author's first message forever.
        Should.Throw<DomainException>(() =>
            Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hi", clientMessageId: Guid.Empty));
    }

    [Fact]
    public void Post_KeepsTheClientMessageId()
    {
        var key = Guid.NewGuid();
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hi", clientMessageId: key).ClientMessageId.ShouldBe(key);
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hi").ClientMessageId.ShouldBeNull();
    }

    [Fact]
    public void Post_KeepsDistinctMentions()
    {
        var u = Guid.NewGuid();
        var message = Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hi", MessageFormat.Markdown, mentions: [u, u]);
        message.Mentions.ShouldHaveSingleItem();
    }

    [Fact]
    public void Edit_SetsEditedAt()
    {
        var message = NewMessage();
        message.Edit("updated");
        message.Body.ShouldBe("updated");
        message.EditedAt.ShouldNotBeNull();
    }

    [Fact]
    public void SoftDelete_ThenEdit_Throws()
    {
        var message = NewMessage();
        message.SoftDelete();
        message.IsDeleted.ShouldBeTrue();
        Should.Throw<DomainException>(() => message.Edit("x"));
    }

    [Fact]
    public void AddReaction_IsIdempotentPerUserEmoji()
    {
        var message = NewMessage();
        var user = Guid.NewGuid();
        message.AddReaction(user, ":+1:");
        message.AddReaction(user, ":+1:");
        message.Reactions.ShouldHaveSingleItem();
    }
}
