namespace HUB.Realtime.WebApi.Presence;

/// <summary>
/// Pure rules for storing a custom status (CF-19), split out from <see cref="RedisPresenceStore"/> so they
/// can be tested without Redis. Decides whether a status should be stored and, if so, with what TTL.
/// </summary>
public static class CustomStatusPolicy
{
    /// <summary>The longest a status is kept when the user chose "don't clear".</summary>
    public static readonly TimeSpan MaxTtl = TimeSpan.FromDays(7);

    /// <summary>The outcome of normalising a requested custom status.</summary>
    /// <param name="Store">The status to persist, or null when the request clears it.</param>
    /// <param name="Ttl">How long to keep it (only meaningful when <see cref="Store"/> is set).</param>
    public readonly record struct Decision(CustomStatus? Store, TimeSpan Ttl);

    /// <summary>
    /// Normalises a requested status: trims, treats an empty note+emoji as a clear, treats an expiry in the
    /// past as a clear, and caps an open-ended status at <see cref="MaxTtl"/>.
    /// </summary>
    /// <param name="requested">The requested status.</param>
    /// <param name="now">The current time (injected for testability).</param>
    /// <returns>A <see cref="Decision"/> the store acts on.</returns>
    public static Decision Evaluate(CustomStatus requested, DateTimeOffset now)
    {
        var emoji = requested.Emoji.Trim();
        var text  = requested.Text.Trim();

        // Nothing to say → clear.
        if (emoji.Length == 0 && text.Length == 0)
            return new Decision(null, TimeSpan.Zero);

        // An expiry already in the past means "clear now".
        if (requested.ExpiresAt is { } exp && exp <= now)
            return new Decision(null, TimeSpan.Zero);

        var ttl = requested.ExpiresAt is { } e ? e - now : MaxTtl;
        if (ttl > MaxTtl) ttl = MaxTtl; // cap "don't clear" so an abandoned note eventually lapses

        return new Decision(new CustomStatus(emoji, text, requested.ExpiresAt), ttl);
    }
}
