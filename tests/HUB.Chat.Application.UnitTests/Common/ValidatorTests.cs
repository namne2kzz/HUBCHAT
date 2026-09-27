using HUB.Chat.Application.Channels.Commands.CreateChannel;
using HUB.Chat.Application.Channels.Commands.OpenLinkedThread;
using HUB.Chat.Application.Messages.Commands.PostMessage;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Common;

/// <summary>
/// Covers the FluentValidation rules that run in the MediatR pipeline before a handler is reached.
/// </summary>
/// <remarks>
/// These decide what comes back as a 400 rather than reaching the domain, so they are the difference
/// between a client being told what to fix and getting a generic failure. The length limits also have to
/// agree with the database columns — a body the validator accepts but the column cannot hold fails at
/// SaveChanges instead, as a 500.
/// </remarks>
public sealed class ValidatorTests
{
    // ── CreateChannel ───────────────────────────────────────────────────────

    private static CreateChannelCommand ValidCreate(string name = "General", string topic = "") =>
        new(Guid.NewGuid(), name, ChannelType.Public, topic, Guid.NewGuid());

    [Fact]
    public void AValidCreateChannelCommandPasses()
    {
        new CreateChannelValidator().Validate(ValidCreate()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AChannelNameIsRequired(string name)
    {
        var result = new CreateChannelValidator().Validate(ValidCreate(name));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateChannelCommand.Name));
    }

    [Fact]
    public void AChannelNameOfExactlyOneHundredCharactersIsAccepted()
    {
        // The boundary itself: MaximumLength is inclusive, and off-by-one here would reject a name the
        // column can hold.
        new CreateChannelValidator().Validate(ValidCreate(new string('a', 100)))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AChannelNameOverOneHundredCharactersIsRejected()
    {
        new CreateChannelValidator().Validate(ValidCreate(new string('a', 101)))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ATopicOfExactlyFiveHundredCharactersIsAccepted()
    {
        new CreateChannelValidator().Validate(ValidCreate(topic: new string('t', 500)))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ATopicOverFiveHundredCharactersIsRejected()
    {
        new CreateChannelValidator().Validate(ValidCreate(topic: new string('t', 501)))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AnEmptyWorkspaceIdIsRejected()
    {
        var command = new CreateChannelCommand(Guid.Empty, "General", ChannelType.Public, "", Guid.NewGuid());

        // Guid.Empty is what an unset or missing id deserialises to, so it has to be caught rather than
        // creating a channel in a workspace that does not exist.
        new CreateChannelValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AnEmptyActingUserIdIsRejected()
    {
        var command = new CreateChannelCommand(Guid.NewGuid(), "General", ChannelType.Public, "", Guid.Empty);

        new CreateChannelValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    // ── PostMessage ─────────────────────────────────────────────────────────

    private static PostMessageCommand ValidPost(string body = "hello") =>
        new(Guid.NewGuid(), body, MessageFormat.Markdown, null, [], Guid.NewGuid());

    [Fact]
    public void AValidPostMessageCommandPasses()
    {
        new PostMessageValidator().Validate(ValidPost()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AMessageBodyIsRequired(string body)
    {
        var result = new PostMessageValidator().Validate(ValidPost(body));

        // Caught here rather than in the domain so the client gets a 400 naming the field instead of a
        // 409 carrying a domain message.
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(PostMessageCommand.Body));
    }

    [Fact]
    public void ABodyOfExactlyEightThousandCharactersIsAccepted()
    {
        new PostMessageValidator().Validate(ValidPost(new string('x', 8000))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ABodyOverEightThousandCharactersIsRejected()
    {
        new PostMessageValidator().Validate(ValidPost(new string('x', 8001))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AnEmptyChannelIdIsRejectedWhenPosting()
    {
        var command = new PostMessageCommand(
            Guid.Empty, "hello", MessageFormat.Plain, null, [], Guid.NewGuid());

        new PostMessageValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AnEmptyAuthorIdIsRejectedWhenPosting()
    {
        var command = new PostMessageCommand(
            Guid.NewGuid(), "hello", MessageFormat.Plain, null, [], Guid.Empty);

        new PostMessageValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void MentionsAreNotValidated()
    {
        // No rule covers MentionedUserIds, so a mention of somebody who does not exist passes here and
        // becomes a UserMentioned event for an id nobody will ever read. Recorded rather than asserted as
        // correct: the notification service simply stores it, so it is harmless today.
        var command = new PostMessageCommand(
            Guid.NewGuid(), "hi", MessageFormat.Plain, null, [Guid.Empty], Guid.NewGuid());

        new PostMessageValidator().Validate(command).IsValid.ShouldBeTrue();
    }

    // ── OpenLinkedThread ────────────────────────────────────────────────────

    [Fact]
    public void AValidOpenLinkedThreadCommandPasses()
    {
        var command = new OpenLinkedThreadCommand(
            Guid.NewGuid(), LinkedResourceType.WorkItem, Guid.NewGuid(),
            "DASH-1", "Title", "http://x", Guid.NewGuid());

        new OpenLinkedThreadValidator().Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AnEmptyExternalIdIsRejected()
    {
        var command = new OpenLinkedThreadCommand(
            Guid.NewGuid(), LinkedResourceType.WorkItem, Guid.Empty,
            "DASH-1", "Title", "http://x", Guid.NewGuid());

        // The domain rejects this too, but catching it here keeps it a 400 rather than a 409 — and a
        // missing resource id means the caller's request was malformed, not that a rule was broken.
        new OpenLinkedThreadValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnExternalKeyIsRequired(string key)
    {
        var command = new OpenLinkedThreadCommand(
            Guid.NewGuid(), LinkedResourceType.WorkItem, Guid.NewGuid(),
            key, "", "http://x", Guid.NewGuid());

        // The key doubles as the channel name when no title is supplied, so an empty one would produce a
        // nameless channel.
        new OpenLinkedThreadValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AnExternalKeyOverSixtyFourCharactersIsRejected()
    {
        var command = new OpenLinkedThreadCommand(
            Guid.NewGuid(), LinkedResourceType.WorkItem, Guid.NewGuid(),
            new string('k', 65), "Title", "http://x", Guid.NewGuid());

        new OpenLinkedThreadValidator().Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AThreadTitleOverOneHundredCharactersIsRejected()
    {
        var command = new OpenLinkedThreadCommand(
            Guid.NewGuid(), LinkedResourceType.WorkItem, Guid.NewGuid(),
            "DASH-1", new string('t', 101), "http://x", Guid.NewGuid());

        // The title becomes the channel name, so this limit has to agree with CreateChannel's 100.
        new OpenLinkedThreadValidator().Validate(command).IsValid.ShouldBeFalse();
    }
}
