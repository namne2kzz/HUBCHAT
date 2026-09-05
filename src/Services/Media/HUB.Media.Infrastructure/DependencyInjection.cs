using HUB.Media.Application.Common.Interfaces;
using HUB.Media.Infrastructure.Messaging;
using HUB.Media.Infrastructure.Persistence;
using HUB.Media.Infrastructure.Storage;
using HUB.Shared.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace HUB.Media.Infrastructure;

/// <summary>Registers media persistence, MinIO storage, and MassTransit (outbox).</summary>
public static class DependencyInjection
{
    /// <summary>Adds EF Core (PostgreSQL), MinIO client, and RabbitMQ with the transactional outbox.</summary>
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MediaDb")
                               ?? throw new InvalidOperationException("Missing connection string 'MediaDb'.");

        services.AddDbContext<MediaDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IMediaDbContext>(sp => sp.GetRequiredService<MediaDbContext>());
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();

        var minio = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>()
                    ?? throw new InvalidOperationException("Missing 'Minio' configuration section.");
        services.AddSingleton(minio);
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(minio.Endpoint)
            .WithCredentials(minio.AccessKey, minio.SecretKey)
            .WithSSL(minio.UseSsl)
            .Build());
        services.AddScoped<IObjectStorage, MinioObjectStorage>();

        var rabbit = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                     ?? throw new InvalidOperationException("Missing 'RabbitMq' configuration section.");

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            bus.AddEntityFrameworkOutbox<MediaDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbit.Host, rabbit.VirtualHost, h => { h.Username(rabbit.User); h.Password(rabbit.Password); });
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
