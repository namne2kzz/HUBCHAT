using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HUB.Shared.Observability;

/// <summary>Wires OpenTelemetry tracing (OTLP → collector → Jaeger) and health-check endpoints.</summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds distributed tracing for ASP.NET Core + outbound HttpClient, exporting via OTLP.
    /// The endpoint is read from the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">Logical service name shown in traces.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHubObservability(this IServiceCollection services, string serviceName)
    {
        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter()); // endpoint from OTEL_EXPORTER_OTLP_ENDPOINT

        return services;
    }

    /// <summary>Maps <c>/health</c> (liveness) and <c>/health/ready</c> (readiness, checks tagged "ready").</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapHubHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
        });
        return app;
    }
}
