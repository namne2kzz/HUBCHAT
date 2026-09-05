using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Shared.Messaging;

/// <summary>Registers MassTransit over HUB's own RabbitMQ broker with sane retry/idempotency defaults.</summary>
public static class MessagingExtensions
{
    /// <summary>
    /// Adds MassTransit + RabbitMQ. Pass <paramref name="registerConsumers"/> to add this service's consumers;
    /// leave null for publish-only services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration containing the <c>RabbitMq</c> section.</param>
    /// <param name="registerConsumers">Optional hook to register consumers on the bus.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHubMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null)
    {
        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                      ?? throw new InvalidOperationException("Missing 'RabbitMq' configuration section.");

        services.AddSingleton(options);

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            registerConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    h.Username(options.User);
                    h.Password(options.Password);
                });

                // Redelivery (spaced) + immediate retry, then dead-letter — protects against transient failures.
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
