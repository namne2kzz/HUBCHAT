using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Notification.Application;

/// <summary>Registers the notification application layer (MediatR handlers + validators).</summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR + FluentValidation for this assembly.</summary>
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
