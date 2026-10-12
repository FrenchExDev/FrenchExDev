namespace FrenchExDev.Net.Vos.Abstractions.Pipeline;

/// <summary>Marker interface for all Vos pipeline requests.</summary>
public interface IVosRequest<TResult> { }

/// <summary>Request that needs a config file loaded.</summary>
public interface IConfigAware
{
    string ConfigPath { get; }
}

/// <summary>Request that mutates the config file (needs locking + save).</summary>
public interface IConfigMutating : IConfigAware
{
    bool Local { get; }
}
