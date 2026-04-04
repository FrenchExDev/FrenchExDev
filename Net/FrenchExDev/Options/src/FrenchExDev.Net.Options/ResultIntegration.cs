namespace FrenchExDev.Net.Options;

using System;
using System.ComponentModel.DataAnnotations;
using FrenchExDev.Net.Result;

/// <summary>
/// Bidirectional conversion extensions between <see cref="Option{T}"/> and <see cref="Result{T}"/>.
/// </summary>
public static class OptionResultExtensions
{
    // -- Option -> Result --

    /// <summary>
    /// Converts an Option to a Result. Some becomes Success, None becomes Failure
    /// with the provided validation error.
    /// </summary>
    public static Result<T> ToResult<T>(
        this Option<T> option,
        string errorMessage)
        where T : notnull
        => option.IsSome
            ? Result<T>.Success(option.Value)
            : Result<T>.Failure(new ValidationResult(errorMessage));

    /// <summary>
    /// Converts an Option to a Result. Some becomes Success, None becomes Failure
    /// with a validation error produced by the factory.
    /// </summary>
    public static Result<T> ToResult<T>(
        this Option<T> option,
        Func<ValidationResult> errorFactory)
        where T : notnull
        => option.IsSome
            ? Result<T>.Success(option.Value)
            : Result<T>.Failure(errorFactory());

    /// <summary>
    /// Converts an Option to a typed-error Result. Some becomes Success, None becomes
    /// Failure with the provided error.
    /// </summary>
    public static Result<T, TError> ToResult<T, TError>(
        this Option<T> option,
        TError error)
        where T : notnull
        where TError : notnull
        => option.IsSome
            ? Result<T, TError>.Success(option.Value)
            : Result<T, TError>.Failure(error);

    /// <summary>
    /// Converts an Option to a typed-error Result. Some becomes Success, None becomes
    /// Failure with an error produced by the factory.
    /// </summary>
    public static Result<T, TError> ToResult<T, TError>(
        this Option<T> option,
        Func<TError> errorFactory)
        where T : notnull
        where TError : notnull
        => option.IsSome
            ? Result<T, TError>.Success(option.Value)
            : Result<T, TError>.Failure(errorFactory());

    // -- Result -> Option --

    /// <summary>
    /// Converts a Result to an Option. Success becomes Some, Failure becomes None.
    /// The error information is discarded.
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result)
        where T : notnull
        => result.IsSuccess ? Option<T>.Some(result.Value!) : Option<T>.None();

    /// <summary>
    /// Converts a typed-error Result to an Option. Success becomes Some, Failure becomes None.
    /// The typed error is discarded.
    /// </summary>
    public static Option<T> ToOption<T, TError>(this Result<T, TError> result)
        where T : notnull
        where TError : notnull
        => result.IsSuccess ? Option<T>.Some(result.Value!) : Option<T>.None();

    // -- Combined operations --

    /// <summary>
    /// Returns the Option's value as a Success Result if Some,
    /// or recovers using the factory if None.
    /// </summary>
    public static Result<T> OrResult<T>(
        this Option<T> option,
        Func<Result<T>> resultFactory)
        where T : notnull
        => option.IsSome ? Result<T>.Success(option.Value) : resultFactory();

    /// <summary>
    /// Maps an Option through a Result-returning function.
    /// If the Option is None, returns None (skips the function).
    /// If the function returns Failure, returns None (error is discarded).
    /// </summary>
    public static Option<TOut> BindResult<T, TOut>(
        this Option<T> option,
        Func<T, Result<TOut>> binder)
        where T : notnull
        where TOut : notnull
        => option.IsSome ? binder(option.Value).ToOption() : Option<TOut>.None();
}
