namespace FrenchExDev.Net.Diem.Aspire.ServiceDefaults;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared service defaults for Aspire-orchestrated Diem services.
/// Configures OpenTelemetry, health checks, resilience.
/// </summary>
public static class DiemServiceDefaultsExtensions
{
    public static IServiceCollection AddDiemServiceDefaults(this IServiceCollection services)
    {
        // Future: AddOpenTelemetry, AddHealthChecks, AddServiceDiscovery
        return services;
    }
}
