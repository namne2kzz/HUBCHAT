using System.Reflection;
using FluentValidation;
using HUB.Chat.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace HUB.Chat.Application;

/// <summary>Registers the chat application layer: MediatR handlers, validators, and the validation pipeline.</summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR + FluentValidation for this assembly.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddChatApplication(this IServiceCollection services)
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
