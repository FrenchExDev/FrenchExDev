namespace FrenchExDev.Net.Guard;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FrenchExDev.Net.Result;

/// <summary>
/// Guard clauses that return <see cref="Result{T}"/> instead of throwing exceptions.
/// Use inside functional pipelines where exceptions are avoided.
/// </summary>
public sealed class GuardToResult
{
    internal GuardToResult() { }

    /// <summary>Guards against null reference values. Returns Failure if null.</summary>
    public Result<T> Null<T>(T? value, string? errorMessage = null) where T : class
        => value is not null
            ? Result<T>.Success(value)
            : Result<T>.Failure(new ValidationResult(errorMessage ?? "Value must not be null."));

    /// <summary>Guards against null nullable value types. Returns Failure if null.</summary>
    public Result<T> NullValue<T>(T? value, string? errorMessage = null) where T : struct
        => value.HasValue
            ? Result<T>.Success(value.Value)
            : Result<T>.Failure(new ValidationResult(errorMessage ?? "Value must not be null."));

    /// <summary>Guards against null or empty strings.</summary>
    public Result<string> NullOrEmpty(string? value, string? errorMessage = null)
    {
        if (value is null)
            return Result<string>.Failure(
                new ValidationResult(errorMessage ?? "Value must not be null."));
        if (value.Length == 0)
            return Result<string>.Failure(
                new ValidationResult(errorMessage ?? "Value must not be empty."));
        return Result<string>.Success(value);
    }

    /// <summary>Guards against null, empty, or whitespace strings.</summary>
    public Result<string> NullOrWhiteSpace(string? value, string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<string>.Failure(
                new ValidationResult(errorMessage ?? "Value must not be null, empty, or whitespace."));
        return Result<string>.Success(value!);
    }

    /// <summary>Guards against values outside [min, max].</summary>
    public Result<T> OutOfRange<T>(T value, T min, T max, string? errorMessage = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            return Result<T>.Failure(
                new ValidationResult(errorMessage ?? $"Value must be between {min} and {max}."));
        return Result<T>.Success(value);
    }

    /// <summary>Guards against negative values.</summary>
    public Result<T> Negative<T>(T value, string? errorMessage = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default!) < 0)
            return Result<T>.Failure(
                new ValidationResult(errorMessage ?? "Value must not be negative."));
        return Result<T>.Success(value);
    }

    /// <summary>Guards against negative or zero values.</summary>
    public Result<T> NegativeOrZero<T>(T value, string? errorMessage = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default!) <= 0)
            return Result<T>.Failure(
                new ValidationResult(errorMessage ?? "Value must be greater than zero."));
        return Result<T>.Success(value);
    }

    /// <summary>Guards against values that fail a custom predicate.</summary>
    public Result<T> InvalidInput<T>(T value, Func<T, bool> predicate, string errorMessage)
        where T : notnull
    {
        if (!predicate(value))
            return Result<T>.Failure(new ValidationResult(errorMessage));
        return Result<T>.Success(value);
    }

    /// <summary>Guards against default(T) values.</summary>
    public Result<T> Default<T>(T value, string? errorMessage = null)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
            return Result<T>.Failure(
                new ValidationResult(errorMessage ?? "Value must not be default."));
        return Result<T>.Success(value);
    }

    /// <summary>Guards against empty GUIDs.</summary>
    public Result<Guid> EmptyGuid(Guid value, string? errorMessage = null)
    {
        if (value == Guid.Empty)
            return Result<Guid>.Failure(
                new ValidationResult(errorMessage ?? "GUID must not be empty."));
        return Result<Guid>.Success(value);
    }
}
