using System.Text;

namespace HUB.Chat.Application.Common.Models;

/// <summary>Opaque keyset cursor over messages ordered by (CreatedAt desc, Id desc).</summary>
/// <param name="CreatedAt">Created timestamp of the boundary message.</param>
/// <param name="Id">Id of the boundary message (tie-breaker).</param>
public readonly record struct MessageCursor(DateTime CreatedAt, Guid Id)
{
    /// <summary>Encodes the cursor to a URL-safe base64 string.</summary>
    public string Encode()
    {
        var raw = $"{CreatedAt.Ticks}:{Id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>Parses an encoded cursor; returns null when the input is null/empty/invalid.</summary>
    /// <param name="value">Encoded cursor string.</param>
    public static MessageCursor? TryDecode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var raw   = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var parts = raw.Split(':', 2);
            if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParse(parts[1], out var id))
                return new MessageCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (FormatException) { /* fall through to null */ }
        return null;
    }
}
