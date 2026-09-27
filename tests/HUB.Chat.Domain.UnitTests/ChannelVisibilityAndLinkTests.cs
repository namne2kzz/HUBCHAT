using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Chat.Domain.UnitTests;

/// <summary>
/// Covers channel visibility changes, the slug derived from the name, archiving, and the link back to a
/// DASHBOARD resource.
/// </summary>
public sealed class ChannelVisibilityAndLinkTests
{
    private static Channel NewChannel(ChannelType type = ChannelType.Public) =>
        Channel.Create(Guid.NewGuid(), "General", type, Guid.NewGuid());

    // ── Visibility ──────────────────────────────────────────────────────────

    [Fact]
    public void APublicChannelCanBeMadePrivate()
    {
        var channel = NewChannel();

        channel.ChangeVisibility(isPrivate: true);

        channel.Type.ShouldBe(ChannelType.Private);

        // Type and IsPrivate are separate fields and both are read elsewhere, so they have to move
        // together — a mismatch would make a private channel look public to whichever one is checked.
        channel.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void APrivateChannelCanBeMadePublic()
    {
        var channel = NewChannel(ChannelType.Private);

        channel.ChangeVisibility(isPrivate: false);

        channel.Type.ShouldBe(ChannelType.Public);
        channel.IsPrivate.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ChannelType.Dm)]
    [InlineData(ChannelType.GroupDm)]
    public void ADirectMessageCannotChangeVisibility(ChannelType type)
    {
        var channel = NewChannel(type);

        // A DM is private by construction. Making one public would expose a private conversation, and
        // making it "private" again would still leave it typed as a DM with no coherent meaning.
        Should.Throw<DomainException>(() => channel.ChangeVisibility(isPrivate: false))
            .Message.ShouldContain("Direct messages");

        channel.Type.ShouldBe(type);
        channel.IsPrivate.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ChannelType.Private)]
    [InlineData(ChannelType.Dm)]
    [InlineData(ChannelType.GroupDm)]
    public void EveryNonPublicTypeStartsOutPrivate(ChannelType type)
    {
        NewChannel(type).IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void ChangingVisibilityStampsTheUpdateTime()
    {
        var channel = NewChannel();
        channel.UpdatedAt.ShouldBeNull();

        channel.ChangeVisibility(isPrivate: true);

        channel.UpdatedAt.ShouldNotBeNull();
    }

    // ── Name and slug ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("General", "general")]
    [InlineData("Team Standup", "team-standup")]
    [InlineData("  Padded  ", "padded")]
    [InlineData("A/B Testing", "a-b-testing")]
    [InlineData("release-2.0", "release-2-0")]
    [InlineData("!!!Loud!!!", "loud")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    public void TheSlugIsDerivedFromTheName(string name, string expected)
    {
        Channel.Create(Guid.NewGuid(), name, ChannelType.Public, Guid.NewGuid())
            .Slug.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Ünïcode", "ünïcode")]
    [InlineData("Tiếng Việt", "tiếng-việt")]
    [InlineData("日本語", "日本語")]
    public void AccentedAndNonLatinNamesKeepTheirCharactersInTheSlug(string name, string expected)
    {
        // Slugify replaces anything that is not a letter or digit, and char.IsLetterOrDigit is true for
        // accented and non-Latin letters — so they survive rather than being transliterated. Documented
        // here because "slug" usually implies ASCII, and the slug is what the unique index on
        // (WorkspaceId, Slug) is built from: a Vietnamese channel name produces a Vietnamese slug.
        Channel.Create(Guid.NewGuid(), name, ChannelType.Public, Guid.NewGuid())
            .Slug.ShouldBe(expected);
    }

    [Fact]
    public void ANameOfOnlyPunctuationYieldsAnEmptySlug()
    {
        // Every character is replaced then trimmed away, so the slug ends up empty. Worth pinning:
        // the configuration puts a unique index on (WorkspaceId, Slug), so a second such channel in
        // the same workspace would collide on the empty string.
        Channel.Create(Guid.NewGuid(), "!!!", ChannelType.Public, Guid.NewGuid())
            .Slug.ShouldBeEmpty();
    }

    [Fact]
    public void RenamingRecomputesTheSlug()
    {
        var channel = NewChannel();

        channel.UpdateInfo("Release Planning", topic: null);

        channel.Name.ShouldBe("Release Planning");
        channel.Slug.ShouldBe("release-planning");
    }

    [Fact]
    public void AnEmptyNameOnUpdateKeepsTheExistingOne()
    {
        var channel = NewChannel();

        channel.UpdateInfo("   ", topic: "new topic");

        // Blank means "not supplied" here, so a partial update that only sets the topic does not wipe
        // the name.
        channel.Name.ShouldBe("General");
        channel.Topic.ShouldBe("new topic");
    }

    [Fact]
    public void ANullTopicKeepsTheExistingOneButAnEmptyStringClearsIt()
    {
        var channel = NewChannel();
        channel.UpdateInfo(name: null, topic: "first topic");

        channel.UpdateInfo(name: null, topic: null);
        channel.Topic.ShouldBe("first topic", "null topic means 'leave it alone'");

        channel.UpdateInfo(name: null, topic: "");
        channel.Topic.ShouldBeEmpty("an empty topic is how the topic is removed");
    }

    [Fact]
    public void TheTopicIsTrimmed()
    {
        var channel = NewChannel();

        channel.UpdateInfo(name: null, topic: "  spaced out  ");

        channel.Topic.ShouldBe("spaced out");
    }

    // ── Archiving ───────────────────────────────────────────────────────────

    [Fact]
    public void ArchivingMakesTheChannelUnwritable()
    {
        var channel = NewChannel();

        channel.IsArchived.ShouldBeFalse();
        channel.EnsureWritable();

        channel.Archive();

        channel.IsArchived.ShouldBeTrue();
        Should.Throw<DomainException>(channel.EnsureWritable);
    }

    [Fact]
    public void ArchivingTwiceIsHarmless()
    {
        var channel = NewChannel();

        channel.Archive();
        channel.Archive();

        channel.IsArchived.ShouldBeTrue();
    }

    [Fact]
    public void ArchivingDoesNotRemoveMembers()
    {
        var channel = NewChannel();
        var member  = Guid.NewGuid();
        channel.AddMember(member);

        channel.Archive();

        // Archiving is read-only, not a teardown: members keep their access to the history.
        channel.HasMember(member).ShouldBeTrue();
        channel.Members.Count.ShouldBe(2);
    }

    // ── Link to a DASHBOARD resource ────────────────────────────────────────

    [Fact]
    public void ANewChannelIsNotLinked()
    {
        var channel = NewChannel();

        channel.IsLinked.ShouldBeFalse();
        channel.LinkType.ShouldBeNull();
        channel.LinkExternalId.ShouldBeNull();
    }

    [Fact]
    public void LinkingRecordsTheResourceAndItsDeepLink()
    {
        var channel    = NewChannel();
        var externalId = Guid.NewGuid();

        channel.LinkTo(LinkedResourceType.WorkItem, externalId, "DASH-142", "http://localhost:4200/work-items/142");

        channel.IsLinked.ShouldBeTrue();
        channel.LinkType.ShouldBe(LinkedResourceType.WorkItem);
        channel.LinkExternalId.ShouldBe(externalId);
        channel.LinkExternalKey.ShouldBe("DASH-142");
        channel.LinkUrl.ShouldBe("http://localhost:4200/work-items/142");
    }

    [Fact]
    public void LinkingToAnEmptyResourceIdFails()
    {
        var channel = NewChannel();

        // Guid.Empty is what an unset id deserialises to, so it has to be rejected rather than stored
        // as a link pointing at nothing.
        Should.Throw<DomainException>(() =>
                channel.LinkTo(LinkedResourceType.WorkItem, Guid.Empty, "DASH-1", "http://x"))
            .Message.ShouldContain("required");

        channel.IsLinked.ShouldBeFalse();
    }

    [Fact]
    public void RelinkingToTheSameResourceIsIdempotent()
    {
        var channel    = NewChannel();
        var externalId = Guid.NewGuid();

        channel.LinkTo(LinkedResourceType.WorkItem, externalId, "DASH-142", "http://x/142");
        channel.LinkTo(LinkedResourceType.WorkItem, externalId, "DASH-142", "http://x/142");

        channel.LinkExternalId.ShouldBe(externalId);
    }

    [Fact]
    public void RelinkingToADifferentResourceReplacesTheLink()
    {
        var channel = NewChannel();
        var first   = Guid.NewGuid();
        var second  = Guid.NewGuid();

        channel.LinkTo(LinkedResourceType.WorkItem, first, "DASH-1", "http://x/1");
        channel.LinkTo(LinkedResourceType.Sprint, second, "SPRINT-9", "http://x/9");

        // A channel carries at most one link; the newer one wins rather than accumulating.
        channel.LinkType.ShouldBe(LinkedResourceType.Sprint);
        channel.LinkExternalId.ShouldBe(second);
        channel.LinkExternalKey.ShouldBe("SPRINT-9");
    }

    [Theory]
    [InlineData(LinkedResourceType.WorkItem)]
    [InlineData(LinkedResourceType.WikiPage)]
    [InlineData(LinkedResourceType.Sprint)]
    public void EveryLinkedResourceTypeCanBeLinked(LinkedResourceType type)
    {
        var channel = NewChannel();

        channel.LinkTo(type, Guid.NewGuid(), "KEY-1", "http://x");

        channel.LinkType.ShouldBe(type);
    }

    [Fact]
    public void ANullKeyOrUrlBecomesAnEmptyString()
    {
        var channel = NewChannel();

        channel.LinkTo(LinkedResourceType.WikiPage, Guid.NewGuid(), null!, null!);

        // Both are non-nullable strings on the entity, so they normalise rather than storing null and
        // breaking the mapping.
        channel.LinkExternalKey.ShouldBeEmpty();
        channel.LinkUrl.ShouldBeEmpty();
    }
}
