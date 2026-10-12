namespace FrenchExDev.Net.Mapper;

/// <summary>
/// Defines a mapper that converts an instance of <typeparamref name="TSource"/>
/// to an instance of <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">The source type to map from.</typeparam>
/// <typeparam name="TTarget">The target type to map to.</typeparam>
public interface IMapper<in TSource, out TTarget>
{
    /// <summary>
    /// Maps a <paramref name="source"/> instance to a new <typeparamref name="TTarget"/> instance.
    /// </summary>
    /// <param name="source">The source object to map.</param>
    /// <returns>A new instance of <typeparamref name="TTarget"/>.</returns>
    TTarget Map(TSource source);
}
