namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>
/// Delegate representing a per-version processing step.
/// </summary>
public delegate Task VersionDelegate(VersionContext ctx);

/// <summary>
/// Composable middleware pipeline for per-version processing.
/// Each middleware wraps the next, enabling setup/teardown via try/finally.
/// </summary>
public sealed class DesignPipeline
{
    private readonly List<Func<VersionDelegate, VersionDelegate>> _middleware = [];

    /// <summary>
    /// Adds a middleware to the pipeline. Middleware added first runs outermost.
    /// </summary>
    public DesignPipeline Use(Func<VersionDelegate, VersionDelegate> middleware)
    {
        _middleware.Add(middleware);
        return this;
    }

    /// <summary>
    /// Builds the middleware chain into a single <see cref="VersionDelegate"/>.
    /// </summary>
    public VersionDelegate Build()
    {
        VersionDelegate terminal = _ => Task.CompletedTask;
        foreach (var mw in _middleware.AsEnumerable().Reverse())
            terminal = mw(terminal);
        return terminal;
    }
}
