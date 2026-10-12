namespace FrenchExDev.Net.Diem;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register all Diem CMF services.
/// </summary>
public static class DiemServiceCollectionExtensions
{
    public static IServiceCollection AddDiem(this IServiceCollection services, Action<DiemCmfOptions>? configure = null)
    {
        var options = new DiemCmfOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        // Future: register sub-DSL services, media, search, identity, caching, etc.
        return services;
    }
}
