using System.Reflection;
using HUB.Chat.Domain.Common;

namespace HUB.TestKit.Builders;

/// <summary>Sets the audit timestamps on a domain entity, which are otherwise assigned by the base class.</summary>
/// <remarks>
/// <c>Entity.CreatedAt</c> is <c>protected set</c> and stamped with <c>DateTime.UtcNow</c> on construction —
/// correct for production and unusable for a pagination test, where rows need known, distinct and
/// deliberately ordered timestamps. Messages created in a loop otherwise land within the same tick or
/// two, which is exactly the ambiguity a keyset test is trying to pin down.
///
/// Reflection is the narrow price of not adding a test-only setter to the domain. It is confined to
/// this one helper so there is a single place to look if the base class changes.
/// </remarks>
public static class EntityTimestamps
{
    private static readonly PropertyInfo CreatedAtProperty =
        typeof(Entity).GetProperty(nameof(Entity.CreatedAt))
        ?? throw new InvalidOperationException($"{nameof(Entity)}.{nameof(Entity.CreatedAt)} is no longer a property.");

    /// <summary>Overwrites an entity's creation timestamp.</summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="entity">Entity to stamp.</param>
    /// <param name="createdAt">The creation time to set.</param>
    /// <returns>The same entity, for chaining.</returns>
    public static TEntity WithCreatedAt<TEntity>(this TEntity entity, DateTime createdAt) where TEntity : Entity
    {
        CreatedAtProperty.SetValue(entity, createdAt);
        return entity;
    }
}
