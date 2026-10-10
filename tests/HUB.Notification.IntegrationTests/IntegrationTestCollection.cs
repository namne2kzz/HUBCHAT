using Xunit;

namespace HUB.Notification.IntegrationTests;

/// <summary>Shares <see cref="PostgresFixture"/> across test classes.</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "notification-postgres";
}
