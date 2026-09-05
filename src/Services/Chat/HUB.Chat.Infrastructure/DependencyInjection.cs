using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Infrastructure.Messaging;
using HUB.Chat.Infrastructure.Persistence;
using HUB.Shared.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Chat.Infrastructure;

/// <summary>Registers the chat infrastructure: EF Core (PostgreSQL), MassTransit + RabbitMQ with the transactional outbox.</summary>
public static class DependencyInjection
{
    /// <summary>Adds persistence and messaging for the chat service.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration (connection string "ChatDb" + "RabbitMq" section).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChatDb")
                               ?? throw new InvalidOperationException("Missing connection string 'ChatDb'.");

        services.AddDbContext<ChatDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IChatDbContext>(sp => sp.GetRequiredService<ChatDbContext>());
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();

        var rabbit = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                     ?? throw new InvalidOperationException("Missing 'RabbitMq' configuration section.");

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            // Transactional outbox stored in ChatDbContext; delivered to RabbitMQ after commit.
            bus.AddEntityFrameworkOutbox<ChatDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbit.Host, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.User);
                    h.Password(rabbit.Password);
                });
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
