using HUB.Chat.Application.Common.Models;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Common;

/// <summary>
/// Covers <c>MessageCursor</c>, the opaque keyset cursor the message list pages on.
/// </summary>
/// <remarks>
/// This is a pure function reached straight from a query string, so the malformed inputs matter as
/// much as the round trip: every one of them arrives from a client eventually, and a cursor that
/// throws instead of returning null turns a stale browser tab into a 500.
/// </remarks>
public sealed class MessageCursorTests
{
    [Fact]
    public void ACursorSurvivesEncodingAndDecoding()
    {
        var original = new MessageCursor(new DateTime(2026, 9, 27, 10, 30, 0, DateTimeKind.Utc), Guid.NewGuid());

        var decoded = MessageCursor.TryDecode(original.Encode());

        decoded.ShouldNotBeNull();
        decoded!.Value.CreatedAt.ShouldBe(original.CreatedAt);
        decoded.Value.Id.ShouldBe(original.Id);
    }

    [Fact]
    public void TheFullTickPrecisionIsPreserved()
    {
        // Ticks, not milliseconds: the cursor is a strict `<` boundary, so a timestamp rounded on the
        // way through would either re-serve the boundary message or skip past it.
        var precise  = new DateTime(637_000_000_123_456_789, DateTimeKind.Utc);
        var original = new MessageCursor(precise, Guid.NewGuid());

        MessageCursor.TryDecode(original.Encode())!.Value.CreatedAt.Ticks.ShouldBe(precise.Ticks);
    }

    [Fact]
    public void ADecodedTimestampIsUtc()
    {
        // The query compares against UTC timestamps from the database. A cursor decoded as Unspecified
        // or Local would shift the page boundary by the server's offset.
        var original = new MessageCursor(DateTime.UtcNow, Guid.NewGuid());

        MessageCursor.TryDecode(original.Encode())!.Value.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAbsentCursorDecodesToNull(string? value)
    {
        // The first page arrives with no cursor at all, so this is the ordinary path, not an error one.
        MessageCursor.TryDecode(value).ShouldBeNull();
    }

    [Theory]
    [InlineData("not-base64-at-all!!")]
    [InlineData("%%%")]
    [InlineData("a")]
    public void MalformedBase64DecodesToNullRatherThanThrowing(string value)
    {
        MessageCursor.TryDecode(value).ShouldBeNull();
    }

    [Theory]
    [InlineData("abc")]                                    // no separator
    [InlineData("notanumber:6f9619ff-8b86-d011-b42d-00c04fc964ff")] // ticks not numeric
    [InlineData("12345:not-a-guid")]                       // id not a guid
    [InlineData("12345")]                                  // ticks only
    [InlineData(":")]                                      // separator only
    public void ValidBase64WithAWrongPayloadDecodesToNull(string payload)
    {
        // Base64 that decodes cleanly but means nothing — the shape a truncated or hand-edited cursor
        // actually takes. TryDecode only catches FormatException, so anything that gets past the
        // base64 step has to be rejected by the parse checks rather than by the catch.
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));

        MessageCursor.TryDecode(encoded).ShouldBeNull();
    }

    [Fact]
    public void ATickCountOutsideDateTimeRangeDecodesToNullRatherThanThrowing()
    {
        // long.MaxValue parses as a number but is not a legal tick count, so the DateTime constructor
        // throws ArgumentOutOfRangeException — which the `catch (FormatException)` does not cover.
        // A client can send this, so it must not become a 500.
        var payload = $"{long.MaxValue}:{Guid.NewGuid()}";
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));

        MessageCursor.TryDecode(encoded).ShouldBeNull();
    }

    [Fact]
    public void AGuidContainingTheSeparatorStillParses()
    {
        // Split(':', 2) caps the parts at two, so the id keeps any colon it contains rather than being
        // cut in half. Guid's own format has none, but this pins the limit as deliberate.
        var original = new MessageCursor(DateTime.UtcNow, Guid.NewGuid());
        var decoded  = MessageCursor.TryDecode(original.Encode());

        decoded!.Value.Id.ShouldBe(original.Id);
    }
}
