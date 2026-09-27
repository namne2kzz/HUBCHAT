using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.UnitTests;

/// <summary>
/// Covers reactions on a message: the one-per-user-per-emoji rule, removal, and what a soft-delete
/// closes off.
/// </summary>
/// <remarks>
/// Reaction identity is the pair (user, emoji), not the user alone. Getting that wrong in either
/// direction is plausible and quiet: too strict and a person cannot react twice with different emoji,
/// too loose and one person can inflate a count without limit.
/// </remarks>
public sealed class MessageReactionTests
{
    private static Message NewMessage() =>
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hello", MessageFormat.Markdown);

    [Fact]
    public void OnePersonMayReactWithSeveralDifferentEmoji()
    {
        var message = NewMessage();
        var user    = Guid.NewGuid();

        message.AddReaction(user, ":+1:");
        message.AddReaction(user, ":tada:");

        // De-duplicating on the user alone would swallow the second one.
        message.Reactions.Count.ShouldBe(2);
    }

    [Fact]
    public void SeveralPeopleMayReactWithTheSameEmoji()
    {
        var message = NewMessage();

        message.AddReaction(Guid.NewGuid(), ":+1:");
        message.AddReaction(Guid.NewGuid(), ":+1:");
        message.AddReaction(Guid.NewGuid(), ":+1:");

        message.Reactions.Count.ShouldBe(3);
    }

    [Fact]
    public void ReactingTwiceWithTheSameEmojiIsIgnoredRatherThanRejected()
    {
        var message = NewMessage();
        var user    = Guid.NewGuid();

        message.AddReaction(user, ":+1:");
        message.AddReaction(user, ":+1:");

        // Silent rather than an exception: a double-click or a retried request should not surface an
        // error to the person clicking.
        message.Reactions.ShouldHaveSingleItem();
    }

    [Fact]
    public void AnEmojiIsRequired()
    {
        var message = NewMessage();

        Should.Throw<DomainException>(() => message.AddReaction(Guid.NewGuid(), "   "))
            .Message.ShouldContain("Emoji is required");
    }

    [Fact]
    public void AReactionRecordsWhoAddedItAndWhichEmoji()
    {
        var message = NewMessage();
        var user    = Guid.NewGuid();

        message.AddReaction(user, ":eyes:");

        var reaction = message.Reactions.ShouldHaveSingleItem();
        reaction.UserId.ShouldBe(user);
        reaction.Emoji.ShouldBe(":eyes:");
        reaction.MessageId.ShouldBe(message.Id);
    }

    [Fact]
    public void RemovingAReactionTakesOffOnlyThatUsersEmoji()
    {
        var message = NewMessage();
        var alice   = Guid.NewGuid();
        var bob     = Guid.NewGuid();

        message.AddReaction(alice, ":+1:");
        message.AddReaction(bob, ":+1:");
        message.AddReaction(alice, ":tada:");

        message.RemoveReaction(alice, ":+1:");

        message.Reactions.Count.ShouldBe(2);
        message.Reactions.ShouldNotContain(r => r.UserId == alice && r.Emoji == ":+1:");
        message.Reactions.ShouldContain(r => r.UserId == bob && r.Emoji == ":+1:");
        message.Reactions.ShouldContain(r => r.UserId == alice && r.Emoji == ":tada:");
    }

    [Fact]
    public void RemovingAReactionThatIsNotThereDoesNothing()
    {
        var message = NewMessage();
        message.AddReaction(Guid.NewGuid(), ":+1:");

        // Idempotent, matching AddReaction: un-clicking twice is not an error.
        message.RemoveReaction(Guid.NewGuid(), ":confused:");

        message.Reactions.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddingAReactionCanBeUndoneAndRedone()
    {
        var message = NewMessage();
        var user    = Guid.NewGuid();

        message.AddReaction(user, ":+1:");
        message.RemoveReaction(user, ":+1:");
        message.AddReaction(user, ":+1:");

        // The de-duplication guard must not outlive the removal, or a person could never react again
        // after changing their mind once.
        message.Reactions.ShouldHaveSingleItem();
    }

    [Fact]
    public void ADeletedMessageCannotBeReactedTo()
    {
        var message = NewMessage();
        message.SoftDelete();

        Should.Throw<DomainException>(() => message.AddReaction(Guid.NewGuid(), ":+1:"))
            .Message.ShouldContain("deleted");
    }

    [Fact]
    public void AReactionCanStillBeRemovedFromADeletedMessage()
    {
        var message = NewMessage();
        var user    = Guid.NewGuid();
        message.AddReaction(user, ":+1:");

        message.SoftDelete();
        message.RemoveReaction(user, ":+1:");

        // Asymmetric with AddReaction, and deliberately so: withdrawing a reaction from something that
        // has since been deleted should not be blocked.
        message.Reactions.ShouldBeEmpty();
    }

    [Fact]
    public void ReactingStampsTheUpdateTime()
    {
        var message = NewMessage();
        message.UpdatedAt.ShouldBeNull();

        message.AddReaction(Guid.NewGuid(), ":+1:");

        message.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void AnIgnoredDuplicateReactionDoesNotStampTheUpdateTime()
    {
        var message = NewMessage();
        var user    = Guid.NewGuid();

        message.AddReaction(user, ":+1:");
        var firstStamp = message.UpdatedAt;

        message.AddReaction(user, ":+1:");

        // Nothing changed, so nothing should look changed. The stamp drives cache invalidation and
        // concurrency, so a no-op that bumps it makes both noisier than they need to be.
        message.UpdatedAt.ShouldBe(firstStamp);
    }
}
