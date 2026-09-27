using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.UnitTests;

/// <summary>
/// Covers a message's construction, its edit and soft-delete lifecycle, mentions, threading fields, and
/// attachments.
/// </summary>
public sealed class MessageLifecycleTests
{
    private static Message NewMessage(string body = "hello") =>
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), body, MessageFormat.Markdown);

    // ── Posting ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void AnEmptyOrWhitespaceBodyIsRejected(string body)
    {
        Should.Throw<DomainException>(() => Message.Post(Guid.NewGuid(), Guid.NewGuid(), body))
            .Message.ShouldContain("cannot be empty");
    }

    [Fact]
    public void TheBodyIsTrimmed()
    {
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "  padded  ").Body.ShouldBe("padded");
    }

    [Fact]
    public void ANewMessageDefaultsToMarkdown()
    {
        // The default matters because the frontend renders on it; a message posted without a format
        // should render the same way as the composer's default.
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "hello").Format.ShouldBe(MessageFormat.Markdown);
    }

    [Fact]
    public void ANewMessageIsNeitherEditedNorDeleted()
    {
        var message = NewMessage();

        message.EditedAt.ShouldBeNull();
        message.DeletedAt.ShouldBeNull();
        message.IsDeleted.ShouldBeFalse();
        message.Reactions.ShouldBeEmpty();
        message.Attachments.ShouldBeEmpty();
        message.Mentions.ShouldBeEmpty();
    }

    [Fact]
    public void ATopLevelMessageHasNoThreadingOrForwardingReferences()
    {
        var message = NewMessage();

        message.ParentId.ShouldBeNull();
        message.ReplyToId.ShouldBeNull();
        message.ForwardedFromId.ShouldBeNull();
    }

    [Fact]
    public void AThreadParentAndAQuoteReplyAreRecordedSeparately()
    {
        var parent  = Guid.NewGuid();
        var quoted  = Guid.NewGuid();

        var message = Message.Post(
            Guid.NewGuid(), Guid.NewGuid(), "both", MessageFormat.Plain,
            parentId: parent, replyToId: quoted);

        // Two different ideas that both look like "replying": ParentId puts the message inside a thread,
        // ReplyToId quotes another message inline. Collapsing them would either flatten threads or make
        // every quote start one.
        message.ParentId.ShouldBe(parent);
        message.ReplyToId.ShouldBe(quoted);
    }

    // ── Mentions ────────────────────────────────────────────────────────────

    [Fact]
    public void MentionsAreDeduplicated()
    {
        var user = Guid.NewGuid();

        var message = Message.Post(
            Guid.NewGuid(), Guid.NewGuid(), "@you @you @you", MessageFormat.Plain, mentions: [user, user, user]);

        // Each mention becomes a notification, so a duplicate here is a duplicate notification.
        message.Mentions.ShouldHaveSingleItem().ShouldBe(user);
    }

    [Fact]
    public void DistinctMentionsAreAllKept()
    {
        Guid[] users = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        var message = Message.Post(
            Guid.NewGuid(), Guid.NewGuid(), "hi all", MessageFormat.Plain, mentions: users);

        message.Mentions.ShouldBe(users);
    }

    [Fact]
    public void MentionOrderIsPreserved()
    {
        Guid[] users = [Guid.NewGuid(), Guid.NewGuid()];

        var message = Message.Post(
            Guid.NewGuid(), Guid.NewGuid(), "a then b", MessageFormat.Plain, mentions: users);

        message.Mentions[0].ShouldBe(users[0]);
        message.Mentions[1].ShouldBe(users[1]);
    }

    [Fact]
    public void ANullMentionListBecomesAnEmptyOne()
    {
        Message.Post(Guid.NewGuid(), Guid.NewGuid(), "nobody", MessageFormat.Plain, mentions: null)
            .Mentions.ShouldBeEmpty();
    }

    [Fact]
    public void EditingDoesNotChangeTheMentionList()
    {
        var mentioned = Guid.NewGuid();
        var message   = Message.Post(
            Guid.NewGuid(), Guid.NewGuid(), "@you", MessageFormat.Plain, mentions: [mentioned]);

        message.Edit("never mind");

        // Edit only takes a body, so the stored mentions no longer match the text. Pinned because it
        // means editing out an @name does not withdraw the notification, and editing one in does not
        // create one — a known shape of the feature, not an accident to be surprised by later.
        message.Mentions.ShouldHaveSingleItem().ShouldBe(mentioned);
    }

    // ── Editing ─────────────────────────────────────────────────────────────

    [Fact]
    public void EditingReplacesTheBodyAndStampsEditedAt()
    {
        var message = NewMessage();

        message.Edit("updated");

        message.Body.ShouldBe("updated");
        message.EditedAt.ShouldNotBeNull();
        message.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void AnEditedBodyIsTrimmed()
    {
        var message = NewMessage();

        message.Edit("  updated  ");

        message.Body.ShouldBe("updated");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEditCannotEmptyTheBody(string body)
    {
        var message = NewMessage("original");

        Should.Throw<DomainException>(() => message.Edit(body));

        // Clearing the text is a delete, and it has to go through SoftDelete so the message stops being
        // listed. A blank body would leave it visible and empty.
        message.Body.ShouldBe("original");
        message.EditedAt.ShouldBeNull();
    }

    [Fact]
    public void ADeletedMessageCannotBeEdited()
    {
        var message = NewMessage();
        message.SoftDelete();

        Should.Throw<DomainException>(() => message.Edit("resurrect"))
            .Message.ShouldContain("deleted");
    }

    [Fact]
    public void EditingRepeatedlyKeepsMovingEditedAt()
    {
        var message = NewMessage();

        message.Edit("first");
        var firstEdit = message.EditedAt;

        message.Edit("second");

        message.Body.ShouldBe("second");
        message.EditedAt!.Value.ShouldBeGreaterThanOrEqualTo(firstEdit!.Value);
    }

    // ── Soft delete ─────────────────────────────────────────────────────────

    [Fact]
    public void SoftDeletingStampsDeletedAtAndKeepsTheBody()
    {
        var message = NewMessage("sensitive");

        message.SoftDelete();

        message.IsDeleted.ShouldBeTrue();
        message.DeletedAt.ShouldNotBeNull();

        // The row is retained — the query layer filters it out. Worth knowing when reasoning about what
        // "delete" means here: the text is still in the database.
        message.Body.ShouldBe("sensitive");
    }

    [Fact]
    public void SoftDeletingTwiceKeepsTheFirstTimestamp()
    {
        var message = NewMessage();

        message.SoftDelete();
        var firstDeletion = message.DeletedAt;

        message.SoftDelete();

        // Idempotent, so a retried delete does not rewrite when it happened.
        message.DeletedAt.ShouldBe(firstDeletion);
    }

    [Fact]
    public void ADeletedMessageKeepsItsExistingReactions()
    {
        var message = NewMessage();
        message.AddReaction(Guid.NewGuid(), ":+1:");

        message.SoftDelete();

        message.Reactions.ShouldHaveSingleItem();
    }

    // ── Attachments ─────────────────────────────────────────────────────────

    [Fact]
    public void AnAttachmentRecordsItsStorageReferenceAndMetadata()
    {
        var message = NewMessage();

        message.AddAttachment(AttachmentKind.Image, "channels/abc/def", "photo.png", 2048, "image/png", 800, 600);

        var attachment = message.Attachments.ShouldHaveSingleItem();
        attachment.MessageId.ShouldBe(message.Id);
        attachment.Kind.ShouldBe(AttachmentKind.Image);
        attachment.Url.ShouldBe("channels/abc/def");
        attachment.Name.ShouldBe("photo.png");
        attachment.Size.ShouldBe(2048);
        attachment.Mime.ShouldBe("image/png");
        attachment.Width.ShouldBe(800);
        attachment.Height.ShouldBe(600);
    }

    [Fact]
    public void AFileAttachmentHasNoDimensions()
    {
        var message = NewMessage();

        message.AddAttachment(AttachmentKind.File, "misc/xyz", "report.pdf", 9000, "application/pdf");

        var attachment = message.Attachments.ShouldHaveSingleItem();
        attachment.Width.ShouldBeNull();
        attachment.Height.ShouldBeNull();
    }

    [Fact]
    public void AMessageMayCarrySeveralAttachments()
    {
        var message = NewMessage();

        message.AddAttachment(AttachmentKind.Image, "k1", "a.png", 1, "image/png");
        message.AddAttachment(AttachmentKind.File, "k2", "b.pdf", 2, "application/pdf");
        message.AddAttachment(AttachmentKind.Video, "k3", "c.mp4", 3, "video/mp4");

        message.Attachments.Count.ShouldBe(3);
        message.Attachments.Select(a => a.Kind)
            .ShouldBe([AttachmentKind.Image, AttachmentKind.File, AttachmentKind.Video]);
    }

    [Fact]
    public void TheSameFileCanBeAttachedTwice()
    {
        var message = NewMessage();

        message.AddAttachment(AttachmentKind.File, "same-key", "dup.pdf", 1, "application/pdf");
        message.AddAttachment(AttachmentKind.File, "same-key", "dup.pdf", 1, "application/pdf");

        // No de-duplication on the storage key. Recorded rather than asserted as correct: if attaching
        // the same upload twice should collapse, this is the test that would change.
        message.Attachments.Count.ShouldBe(2);
    }
}
