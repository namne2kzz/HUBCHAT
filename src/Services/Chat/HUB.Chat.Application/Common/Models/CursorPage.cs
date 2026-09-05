namespace HUB.Chat.Application.Common.Models;

/// <summary>A page of items with an opaque cursor for keyset (seek) pagination.</summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items in this page.</param>
/// <param name="NextCursor">Cursor to fetch the next (older) page; null when there are no more.</param>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
