using System;

namespace FrenchExDev.Net.Mapper.Testing;

/// <summary>
/// Exception thrown when a mapper assertion fails.
/// </summary>
public sealed class MapperAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapperAssertionException"/> class.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public MapperAssertionException(string message) : base(message)
    {
    }
}
