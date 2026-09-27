using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HUB.Chat.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real Chat API in-process against the PostgreSQL container, replacing only what reaches
/// outside the process.
/// </summary>
/// <remarks>
/// The point of these tests is the genuine pipeline — routing, model binding, the JWT handler, the
/// <c>[Authorize]</c> filters, the exception middleware, MediatR behaviours, EF Core against real
/// PostgreSQL. So the substitutions are deliberately few: only RabbitMQ, which is a hosted service that
/// would dial a broker that is not running.
///
/// Each test isolates itself by data — a fresh workspace and fresh user ids — rather than by database,
/// because the container and its migrated schema are shared across the whole run.
/// </remarks>
/// <param name="database">The running PostgreSQL fixture.</param>
public sealed class ApiFactory(PostgresFixture database) : WebApplicationFactory<Program>
{
    /// <summary>The shared token the <c>/internal/*</c> routes expect during tests.</summary>
    public const string InternalToken = "integration-test-internal-token-min-32-chars";

    /// <summary>The HMAC secret test tokens are signed with.</summary>
    public const string JwtSecret = "integration-test-signing-key-at-least-32-bytes-long";

    /// <summary>Issuer the API is configured to accept.</summary>
    public const string JwtIssuer = "dashboard";

    /// <summary>Audience the API is configured to accept.</summary>
    public const string JwtAudience = "dashboard-clients";

    private Dictionary<string, string?> HostSettings => new()
    {
        ["ConnectionStrings:ChatDb"] = database.ConnectionString,

        ["Jwt:Secret"]   = JwtSecret,
        ["Jwt:Issuer"]   = JwtIssuer,
        ["Jwt:Audience"] = JwtAudience,

        // RabbitMQ never connects here (the bus is removed below), but AddChatInfrastructure throws
        // if the section is missing, so it has to be present for the host to compose at all.
        ["RabbitMq:Host"]        = "localhost",
        ["RabbitMq:VirtualHost"] = "/",
        ["RabbitMq:User"]        = "guest",
        ["RabbitMq:Password"]    = "guest",

        ["InternalApi:Token"] = InternalToken,
    };

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs calls AddHubJwtAuth(builder.Configuration) while composing the host, and that reads
        // Jwt:Secret straight away to build the IssuerSigningKey. That read happens BEFORE the
        // ConfigureAppConfiguration below runs, so these values have to arrive through UseSetting as
        // well. Supplying them only via ConfigureAppConfiguration is the mistake that cost the sibling
        // DASHBOARD repository a debugging session: every authenticated request came back 401 "The
        // signature key was not found", because tokens were signed with the test key and validated
        // against the empty one from appsettings.
        foreach (var (key, value) in HostSettings)
            builder.UseSetting(key, value);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Repeated so anything resolving configuration at request time sees the same values.
            config.AddInMemoryCollection(HostSettings);
        });

        builder.ConfigureServices(services =>
        {
            ReplaceDbContext(services);
            RemoveMessageBroker(services);
        });
    }

    private void ReplaceDbContext(IServiceCollection services)
    {
        // AddChatInfrastructure already registered a context from configuration. Re-registering here
        // keeps the pointing explicit and survives a change in how the connection string is read.
        services.RemoveAll<DbContextOptions<ChatDbContext>>();
        services.RemoveAll<ChatDbContext>();

        services.AddDbContext<ChatDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<IChatDbContext>(sp => sp.GetRequiredService<ChatDbContext>());
    }

    /// <summary>Removes MassTransit and records integration events at the application's own seam.</summary>
    /// <remarks>
    /// The bus is a hosted service that dials RabbitMQ on start-up, so leaving it registered makes every
    /// request wait on a connection that will not arrive.
    ///
    /// The recording is done by replacing <see cref="IIntegrationEventPublisher"/> rather than
    /// <c>IPublishEndpoint</c>. That interface is the application's own one-method seam, while
    /// <c>IPublishEndpoint</c> carries a dozen <c>Publish</c> overloads plus observer hooks — hand-writing
    /// it would be a lot of surface area for a test double, and it would break on a MassTransit upgrade
    /// that adds an overload.
    ///
    /// Note what this costs: with the bus gone, so is the EF outbox interceptor, so these tests show that
    /// an endpoint publishes, not that publish and commit share a transaction. That ordering is asserted
    /// in the unit suite instead, by <c>PostMessageHandlerTests</c>.
    /// </remarks>
    private static void RemoveMessageBroker(IServiceCollection services)
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true
                              || d.ImplementationType?.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true)
                     .ToList())
        {
            services.Remove(descriptor);
        }

        // AddMassTransit registers the bus as a hosted service; that has to go too or start-up hangs.
        foreach (var hosted in services
                     .Where(d => d.ServiceType == typeof(IHostedService)
                              && d.ImplementationType?.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true)
                     .ToList())
        {
            services.Remove(hosted);
        }

        // Singleton so a test can read what a request published after the response has come back; the
        // production registration is scoped, which would dispose it with the request.
        services.RemoveAll<IIntegrationEventPublisher>();
        services.AddSingleton<RecordingEventCollector>();
        services.AddSingleton<IIntegrationEventPublisher>(sp => sp.GetRequiredService<RecordingEventCollector>());
    }
}
