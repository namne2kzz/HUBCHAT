using HUB.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HUB.Media.WebApi;

/// <summary>
/// Design-time factory — lets <c>dotnet ef</c> create <see cref="MediaDbContext"/> without the full DI stack.
/// Builds configuration the same way ASP.NET Core does: appsettings.json → appsettings.{env}.json → env vars.
/// Real credentials stay in appsettings.Development.json (git-ignored); no secrets in source code.
/// </summary>
public sealed class MediaDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    /// <inheritdoc />
    public MediaDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var opts = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(config.GetConnectionString("MediaDb"))
            .Options;
        return new MediaDbContext(opts);
    }
}
