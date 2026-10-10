using HUB.Notification.Application.Common.Exceptions;
using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Infrastructure.Consumers;
using HUB.Notification.Infrastructure.Email;
using HUB.Notification.Infrastructure.Messaging;
using HUB.Notification.Infrastructure.Persistence;
using HUB.Shared.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Notification.Infrastructure;

/// <summary>Registers notification persistence + messaging (inbox/outbox consumers).</summary>
public static class DependencyInjection
{
    /// <summary>Adds EF Core (PostgreSQL), the email sender, and the RabbitMQ consumers with inbox + outbox.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration ("NotificationDb" connection string + "RabbitMq" section).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
                               ?? throw new InvalidOperationException("Missing connection string 'NotificationDb'.");

        services.AddDbContext<NotificationDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();

        services.AddHubMessaging(
            configuration,
            bus => bus.AddNotificationConsumers(),
            configureRetry: ConfigureRetry);

        return services;
    }

    /// <summary>
    /// Registers the transactional outbox, the inbox on every receive endpoint, and this service's consumers.
    /// Public so integration tests run the exact production bus wiring on a test harness.
    /// </summary>
    /// <param name="bus">The bus registration configurator.</param>
    /// <returns>The same configurator for chaining.</returns>
    public static IBusRegistrationConfigurator AddNotificationConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddEntityFrameworkOutbox<NotificationDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });

        // Inbox + consumer outbox on every endpoint: the InboxState row (dedupe by MessageId), the consumer's
        // writes and any events it publishes commit in ONE transaction. Registered after the HUB retry
        // callback, so it sits inside the retry — each attempt is a fresh transaction rather than a retry
        // inside an aborted one.
        bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            endpoint.UseEntityFrameworkOutbox<NotificationDbContext>(context));

        bus.AddConsumer<UserMentionedConsumer>();
        bus.AddConsumer<NotificationCreatedEmailConsumer>();
        return bus;
    }

    /// <summary>Retry additions for this service (on top of the HUB policy).</summary>
    /// <param name="retry">The retry configurator.</param>
    /// <remarks>
    /// Hitting the unique index means another message already created this notification: retrying cannot
    /// succeed, so fault it straight to _error (visible) instead of five retries.
    /// </remarks>
    public static void ConfigureRetry(IRetryConfigurator retry) =>
        retry.Ignore<UniqueConstraintViolationException>();
}
