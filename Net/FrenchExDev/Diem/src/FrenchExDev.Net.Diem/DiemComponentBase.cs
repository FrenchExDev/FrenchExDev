namespace FrenchExDev.Net.Diem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for all Diem components (admin modules, front modules, widgets).
/// Provides access to services, logging, and the CMF pipeline.
/// Layer 1 of the 4-layer customization model.
/// </summary>
public abstract class DiemComponentBase
{
    protected IServiceProvider Services { get; }
    protected ILogger Logger { get; }

    protected DiemComponentBase(IServiceProvider services, ILogger logger)
    {
        Services = services;
        Logger = logger;
    }

    protected T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
}
