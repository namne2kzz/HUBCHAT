namespace HUB.Chat.IntegrationTests.Infrastructure;

/// <summary>
/// Names the trait every test class in this project carries.
/// </summary>
/// <remarks>
/// All of these tests start a PostgreSQL container, so none can run without a Docker daemon. Tagging
/// them lets a machine or CI job without Docker exclude the whole project in one filter —
/// <c>dotnet test --filter Category!=RequiresDocker</c> — and still run the unit suites, rather than
/// having the run fail on a daemon that was never going to be there.
/// </remarks>
public static class TestCategories
{
    /// <summary>Trait name.</summary>
    public const string Category = "Category";

    /// <summary>Trait value for tests that need a Docker daemon.</summary>
    public const string RequiresDocker = "RequiresDocker";
}
