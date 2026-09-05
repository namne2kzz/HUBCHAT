using System.Reflection;
using FluentValidation;
using HUB.Media.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Media.Application;

/// <summary>Registers the media application layer (MediatR + validators + validation pipeline).</summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR + FluentValidation for this assembly.</summary>
    public static IServiceCollection AddMediaApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
