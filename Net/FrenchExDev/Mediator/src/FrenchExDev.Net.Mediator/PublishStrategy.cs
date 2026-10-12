namespace FrenchExDev.Net.Mediator;

/// <summary>
/// Determines how notifications are dispatched to their handlers.
/// </summary>
public enum PublishStrategy
{
    /// <summary>
    /// Handlers are invoked one after another, awaiting each before proceeding.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// All handlers are started concurrently and awaited together.
    /// </summary>
    Parallel = 1,

    /// <summary>
    /// All handlers are started but not awaited; exceptions are swallowed.
    /// </summary>
    FireAndForget = 2
}
