namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>Reusable query specification (DDD pattern).</summary>
public interface ISpecification<T> where T : class
{
    Expression<Func<T, bool>> Criteria { get; }

    List<Expression<Func<T, object>>> Includes { get; }

    Expression<Func<T, object>>? OrderBy { get; }

    Expression<Func<T, object>>? OrderByDescending { get; }

    int? Take { get; }

    int? Skip { get; }
}
