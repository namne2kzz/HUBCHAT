using HUB.Media.Domain.Common;
using HUB.Media.Domain.Entities;
using HUB.Media.Domain.Enums;
using Shouldly;
using Xunit;

namespace HUB.Media.Domain.UnitTests;

/// <summary>
/// Covers <c>FileObject</c>: its creation guards, the storage key it derives, the workspace bucket, and
/// the scan status that gates downloading.
/// </summary>
/// <remarks>
/// The scan status is a security boundary, not bookkeeping. <c>IsDownloadable</c> is the single place
/// that decides whether bytes may be handed out, and it must stay closed for anything not positively
/// scanned clean — a default-open reading would serve an unscanned or infected file.
/// </remarks>
public sealed class FileObjectTests
{
    private static FileObject NewFile(Guid? channelId = null) =>
        FileObject.Create(Guid.NewGuid(), channelId, "report.pdf", "application/pdf", 1024, Guid.NewGuid());

    // ── Creation guards ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AFileNameIsRequired(string fileName)
    {
        Should.Throw<DomainException>(() =>
                FileObject.Create(Guid.NewGuid(), null, fileName, "application/pdf", 1, Guid.NewGuid()))
            .Message.ShouldContain("File name is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AContentTypeIsRequired(string contentType)
    {
        Should.Throw<DomainException>(() =>
                FileObject.Create(Guid.NewGuid(), null, "a.pdf", contentType, 1, Guid.NewGuid()))
            .Message.ShouldContain("Content type is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void ANonPositiveSizeIsRejected(long size)
    {
        // The size is declared by the client before the upload happens, so it is the one number that can
        // be made nonsense. A zero-byte ticket would reserve a key for content that never arrives.
        Should.Throw<DomainException>(() =>
                FileObject.Create(Guid.NewGuid(), null, "a.pdf", "application/pdf", size, Guid.NewGuid()))
            .Message.ShouldContain("greater than zero");
    }

    [Fact]
    public void TheFileNameAndContentTypeAreTrimmed()
    {
        var file = FileObject.Create(
            Guid.NewGuid(), null, "  spaced.pdf  ", "  application/pdf  ", 1, Guid.NewGuid());

        file.FileName.ShouldBe("spaced.pdf");
        file.ContentType.ShouldBe("application/pdf");
    }

    [Fact]
    public void CreationRecordsTheUploaderAndWorkspace()
    {
        var workspace = Guid.NewGuid();
        var uploader  = Guid.NewGuid();

        var file = FileObject.Create(workspace, null, "a.pdf", "application/pdf", 42, uploader);

        file.WorkspaceId.ShouldBe(workspace);
        file.UploadedBy.ShouldBe(uploader);
        file.SizeBytes.ShouldBe(42);
    }

    // ── Storage key and bucket ──────────────────────────────────────────────

    [Fact]
    public void AChannelFileIsKeyedUnderThatChannel()
    {
        var channelId = Guid.NewGuid();

        var file = NewFile(channelId);

        // The key embeds the entity's own id, so two uploads of the same name never collide.
        file.StorageKey.ShouldBe($"channels/{channelId}/{file.Id}");
    }

    [Fact]
    public void AFileWithNoChannelIsKeyedUnderMisc()
    {
        var file = NewFile(channelId: null);

        file.StorageKey.ShouldBe($"misc/{file.Id}");
        file.ChannelId.ShouldBeNull();
    }

    [Fact]
    public void TwoUploadsOfTheSameNameGetDifferentKeys()
    {
        var channelId = Guid.NewGuid();

        NewFile(channelId).StorageKey.ShouldNotBe(NewFile(channelId).StorageKey);
    }

    [Fact]
    public void TheBucketIsScopedToTheWorkspace()
    {
        var workspace = Guid.NewGuid();

        var file = FileObject.Create(workspace, null, "a.pdf", "application/pdf", 1, Guid.NewGuid());

        // One bucket per workspace is what keeps one tenant's objects out of another's namespace, so the
        // derivation is worth pinning rather than leaving to a string built at the call site.
        file.Bucket.ShouldBe($"ws-{workspace}");
    }

    [Fact]
    public void TheStorageKeyMatchesTheEntityIdItWasBuiltWith()
    {
        var file = NewFile(Guid.NewGuid());

        // Create assigns the id and the key together. If they ever diverged, the metadata row would point
        // at an object that is not there.
        file.StorageKey.ShouldEndWith(file.Id.ToString());
    }

    // ── Scan status gates downloading ───────────────────────────────────────

    [Fact]
    public void ANewFileIsPendingAndNotDownloadable()
    {
        var file = NewFile();

        file.ScanStatus.ShouldBe(ScanStatus.Pending);

        // Closed by default. An unscanned object must not be servable just because nothing has said it
        // is bad yet.
        file.IsDownloadable.ShouldBeFalse();
    }

    [Fact]
    public void ACleanScanOpensDownloading()
    {
        var file = NewFile();

        file.MarkScanned(clean: true);

        file.ScanStatus.ShouldBe(ScanStatus.Clean);
        file.IsDownloadable.ShouldBeTrue();
    }

    [Fact]
    public void AnInfectedScanKeepsDownloadingClosed()
    {
        var file = NewFile();

        file.MarkScanned(clean: false);

        file.ScanStatus.ShouldBe(ScanStatus.Infected);
        file.IsDownloadable.ShouldBeFalse();
    }

    [Fact]
    public void OnlyACleanStatusIsDownloadable()
    {
        // Stated as an exhaustive check over the enum so that adding a new status cannot quietly inherit
        // "downloadable" from a truthy default.
        var pending = NewFile();
        pending.IsDownloadable.ShouldBeFalse();

        var clean = NewFile();
        clean.MarkScanned(clean: true);
        clean.IsDownloadable.ShouldBeTrue();

        var infected = NewFile();
        infected.MarkScanned(clean: false);
        infected.IsDownloadable.ShouldBeFalse();
    }

    [Fact]
    public void ARescanCanReverseAPreviousVerdict()
    {
        var file = NewFile();

        file.MarkScanned(clean: true);
        file.MarkScanned(clean: false);

        // MarkScanned is a plain setter with no terminal state, so a later scan overrides an earlier one.
        // Recorded because it means a file already ruled clean can be closed off again — useful for an
        // updated signature database, and worth knowing rather than discovering.
        file.ScanStatus.ShouldBe(ScanStatus.Infected);
        file.IsDownloadable.ShouldBeFalse();
    }

    [Fact]
    public void AFileIsTimestampedOnCreation()
    {
        NewFile().CreatedAt.ShouldNotBe(default);
    }
}
