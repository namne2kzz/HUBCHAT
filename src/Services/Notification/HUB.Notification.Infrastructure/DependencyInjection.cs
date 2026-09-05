using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Infrastructure.Consumers;
using HUB.Notification.Infrastructure.Email;
using HUB.Notification.Infrastructure.Persistence;
using HUB.Shared.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Notification.Infrastructure;

/// <summary>Registers notification persistence + messaging (consume UserMentioned).</summary>
public static class DependencyInjection
{
    /// <summary>Adds EF Core (PostgreSQL), the email sender, and the RabbitMQ consumer.</summary>
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
                               ?? throw new InvalidOperationException("Missing connection string 'NotificationDb'.");

        services.AddDbContext<NotificationDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddScoped<IEmailSender, NoOpEmailSender>();

        services.AddHubMessaging(configuration, bus => bus.AddConsumer<UserMentionedConsumer>());
        return services;
    }
}
