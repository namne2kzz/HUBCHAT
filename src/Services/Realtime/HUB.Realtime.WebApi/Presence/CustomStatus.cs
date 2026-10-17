namespace HUB.Realtime.WebApi.Presence;

/// <summary>
/// A user's custom status (CF-19): a short note shown beside their name, optionally with an emoji and an
/// auto-clear time. Independent of <see cref="PresenceStatus"/> — you can be Online and "🍔 at lunch" at once.
/// </summary>
/// <param name="Emoji">A single emoji, or empty.</param>
/// <param name="Text">The status note (already trimmed/length-capped by the caller).</param>
/// <param name="ExpiresAt">When the status auto-clears; null means it stays until the user clears it.</param>
public sealed record CustomStatus(string Emoji, string Text, DateTimeOffset? ExpiresAt);
