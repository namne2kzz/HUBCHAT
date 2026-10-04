using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Shared.Messaging;

/// <summary>Registers MassTransit over HUB's RabbitMQ broker with one retry policy shared by every service.</summary>
public static class MessagingExtensions
{
    /// <summary>How many times a failing message is re-run in memory before it is faulted to <c>_error</c>.</summary>
    public const int RetryLimit = 5;

    /// <summary>
    /// Adds MassTransit + RabbitMQ with the HUB retry policy on every receive endpoint.
    /// </summary>
    /// <remarks>
    /// The single place a HUB service wires its bus — publish-only, consuming, or outbox-backed. A service
    /// with a transactional outbox registers it through <paramref name="configureBus"/>
    /// (<c>bus.AddEntityFrameworkOutbox&lt;TDbContext&gt;(...)</c>) rather than calling
    /// <c>AddMassTransit</c> itself, so retry and endpoint naming cannot drift between services.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration containing the <c>RabbitMq</c> section.</param>
    /// <param name="configureBus">Optional hook to register consumers and/or the EF outbox on the bus.</param>
    /// <param name="configureRetry">
    /// Optional hook to extend the retry policy — typically <c>r.Ignore&lt;DomainException&gt;()</c> for
    /// the service's own non-transient exceptions, which no amount of retrying will fix.
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHubMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null,
        Action<IRetryConfigurator>? configureRetry = null)
    {
        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                      ?? throw new InvalidOperationException("Missing 'RabbitMq' configuration section.");

        services.AddSingleton(options);

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            bus.AddHubRetryPolicy(configureRetry);
            configureBus?.Invoke(bus);

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    h.Username(options.User);
                    h.Password(options.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>Applies the HUB retry policy to every receive endpoint configured by <c>ConfigureEndpoints</c>.</summary>
    /// <remarks>
    /// <para>
    /// Configured per endpoint rather than on the bus so it sits in the consumer pipeline, where an
    /// in-consumer outbox (when one is added) must be layered inside it.
    /// </para>
    /// <para>
    /// In-memory exponential retry only — no delayed redelivery. Delayed redelivery on RabbitMQ needs the
    /// <c>rabbitmq_delayed_message_exchange</c> plugin, which the broker HUB shares with DASHBOARD does not
    /// have; configured anyway, the redelivery step itself fails and the message lands in <c>_error</c>
    /// without the retries it was promised. A message still failing after <see cref="RetryLimit"/> attempts
    /// is faulted to <c>_error</c> for manual inspection.
    /// </para>
    /// <para>Public so tests built on <c>AddMassTransitTestHarness</c> can run the exact production policy.</para>
    /// </remarks>
    /// <param name="bus">The bus registration configurator.</param>
    /// <param name="configureRetry">Optional hook to extend the policy (e.g. ignore non-transient exceptions).</param>
    /// <returns>The same configurator for chaining.</returns>
    public static IBusRegistrationConfigurator AddHubRetryPolicy(
        this IBusRegistrationConfigurator bus,
        Action<IRetryConfigurator>? configureRetry = null)
    {
        bus.AddConfigureEndpointsCallback((_, endpoint) =>
            endpoint.UseMessageRetry(r =>
            {
                // ~0.2s, 0.7s, 1.7s, 3.7s, 7.7s — rides out a broker/DB blip without holding a prefetch
                // slot for minutes.
                r.Exponential(RetryLimit, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500));

                // A cancelled consume means the host is stopping; the broker redelivers it to the next one.
                r.Ignore<OperationCanceledException>();

                configureRetry?.Invoke(r);
            }));

        return bus;
    }
}
