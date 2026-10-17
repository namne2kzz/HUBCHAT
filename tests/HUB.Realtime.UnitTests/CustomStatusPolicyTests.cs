using HUB.Realtime.WebApi.Presence;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers the custom-status rules (CF-19). These are the decisions that matter regardless of Redis:
/// when a status is really a "clear", and how long an open-ended one is allowed to live.
/// </summary>
public sealed class CustomStatusPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEmptyNoteAndEmojiClears()
    {
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("  ", "   ", null), Now);
        d.Store.ShouldBeNull();
    }

    [Fact]
    public void AnExpiryInThePastClears()
    {
        // Picking "clear after 30m" on a status whose clock already passed must not resurrect it.
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("📅", "In a meeting", Now.AddMinutes(-1)), Now);
        d.Store.ShouldBeNull();
    }

    [Fact]
    public void ANoteWithNoExpiryIsCappedAtSevenDays()
    {
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("🏠", "Working remotely", null), Now);

        d.Store.ShouldNotBeNull();
        d.Ttl.ShouldBe(CustomStatusPolicy.MaxTtl);
    }

    [Fact]
    public void AFutureExpiryBecomesItsRemainingTtl()
    {
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("🍔", "Lunch", Now.AddMinutes(30)), Now);

        d.Store!.Text.ShouldBe("Lunch");
        d.Ttl.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AnExpiryFartherThanSevenDaysIsCapped()
    {
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("🌴", "On holiday", Now.AddDays(30)), Now);

        d.Store.ShouldNotBeNull();
        d.Ttl.ShouldBe(CustomStatusPolicy.MaxTtl);
    }

    [Fact]
    public void ItTrimsEmojiAndText()
    {
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("  📅 ", "  heads down  ", null), Now);

        d.Store!.Emoji.ShouldBe("📅");
        d.Store!.Text.ShouldBe("heads down");
    }

    [Fact]
    public void AnEmojiOnlyStatusIsKept()
    {
        // A bare emoji (no words) is a valid Slack status.
        var d = CustomStatusPolicy.Evaluate(new CustomStatus("🎧", "", null), Now);

        d.Store.ShouldNotBeNull();
        d.Store!.Emoji.ShouldBe("🎧");
    }
}
