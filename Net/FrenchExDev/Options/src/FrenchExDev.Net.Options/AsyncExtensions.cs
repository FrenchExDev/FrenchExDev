namespace FrenchExDev.Net.Options;

using System;
using System.Threading.Tasks;

/// <summary>
/// Async extensions for <see cref="Option{T}"/> and <see cref="Task{T}"/>
/// of <see cref="Option{T}"/>, enabling async pipelines without intermediate awaits.
/// </summary>
public static class OptionAsyncExtensions
{
    // -- On Option<T> with async lambdas --

    /// <summary>Async match on an Option.</summary>
    public static Task<TResult> MatchAsync<T, TResult>(
        this Option<T> option,
        Func<T, Task<TResult>> onSome,
        Func<Task<TResult>> onNone)
        where T : notnull
        => option.IsSome ? onSome(option.Value) : onNone();

    /// <summary>Async map on an Option.</summary>
    public static async Task<Option<TOut>> MapAsync<T, TOut>(
        this Option<T> option,
        Func<T, Task<TOut>> mapper)
        where T : notnull
        where TOut : notnull
        => option.IsSome ? Option<TOut>.Some(await mapper(option.Value)) : Option<TOut>.None();

    /// <summary>Async bind on an Option.</summary>
    public static Task<Option<TOut>> BindAsync<T, TOut>(
        this Option<T> option,
        Func<T, Task<Option<TOut>>> binder)
        where T : notnull
        where TOut : notnull
        => option.IsSome ? binder(option.Value) : Task.FromResult(Option<TOut>.None());

    /// <summary>Async tap on an Option.</summary>
    public static async Task<Option<T>> TapAsync<T>(
        this Option<T> option,
        Func<T, Task> action)
        where T : notnull
    {
        if (option.IsSome) await action(option.Value);
        return option;
    }

    // -- On Task<Option<T>> pipeline --

    /// <summary>Chains a sync map onto an async option pipeline.</summary>
    public static async Task<Option<TOut>> MapAsync<T, TOut>(
        this Task<Option<T>> optionTask,
        Func<T, TOut> mapper)
        where T : notnull
        where TOut : notnull
    {
        var option = await optionTask;
        return option.Map(mapper);
    }

    /// <summary>Chains an async map onto an async option pipeline.</summary>
    public static async Task<Option<TOut>> MapAsync<T, TOut>(
        this Task<Option<T>> optionTask,
        Func<T, Task<TOut>> mapper)
        where T : notnull
        where TOut : notnull
    {
        var option = await optionTask;
        return option.IsSome ? Option<TOut>.Some(await mapper(option.Value)) : Option<TOut>.None();
    }

    /// <summary>Chains a sync bind onto an async option pipeline.</summary>
    public static async Task<Option<TOut>> BindAsync<T, TOut>(
        this Task<Option<T>> optionTask,
        Func<T, Option<TOut>> binder)
        where T : notnull
        where TOut : notnull
    {
        var option = await optionTask;
        return option.Bind(binder);
    }

    /// <summary>Chains an async bind onto an async option pipeline.</summary>
    public static async Task<Option<TOut>> BindAsync<T, TOut>(
        this Task<Option<T>> optionTask,
        Func<T, Task<Option<TOut>>> binder)
        where T : notnull
        where TOut : notnull
    {
        var option = await optionTask;
        return option.IsSome ? await binder(option.Value) : Option<TOut>.None();
    }

    /// <summary>Chains a sync tap onto an async option pipeline.</summary>
    public static async Task<Option<T>> TapAsync<T>(
        this Task<Option<T>> optionTask,
        Action<T> action)
        where T : notnull
    {
        var option = await optionTask;
        return option.Tap(action);
    }

    /// <summary>Chains an async tap onto an async option pipeline.</summary>
    public static async Task<Option<T>> TapAsync<T>(
        this Task<Option<T>> optionTask,
        Func<T, Task> action)
        where T : notnull
    {
        var option = await optionTask;
        if (option.IsSome) await action(option.Value);
        return option;
    }

    /// <summary>Chains a filter onto an async option pipeline.</summary>
    public static async Task<Option<T>> WhereAsync<T>(
        this Task<Option<T>> optionTask,
        Func<T, bool> predicate)
        where T : notnull
    {
        var option = await optionTask;
        return option.Filter(predicate);
    }

    /// <summary>Async match on an async option pipeline.</summary>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Option<T>> optionTask,
        Func<T, TResult> onSome,
        Func<TResult> onNone)
        where T : notnull
    {
        var option = await optionTask;
        return option.Match(onSome, onNone);
    }

    /// <summary>Async match with async branches on an async option pipeline.</summary>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Option<T>> optionTask,
        Func<T, Task<TResult>> onSome,
        Func<Task<TResult>> onNone)
        where T : notnull
    {
        var option = await optionTask;
        return option.IsSome ? await onSome(option.Value) : await onNone();
    }

    /// <summary>Unwraps an async option with a fallback.</summary>
    public static async Task<T> OrDefaultAsync<T>(
        this Task<Option<T>> optionTask,
        T fallback)
        where T : notnull
    {
        var option = await optionTask;
        return option.OrDefault(fallback);
    }

    /// <summary>Unwraps an async option with a lazy fallback.</summary>
    public static async Task<T> OrElseAsync<T>(
        this Task<Option<T>> optionTask,
        Func<T> fallbackFactory)
        where T : notnull
    {
        var option = await optionTask;
        return option.OrElse(fallbackFactory);
    }
}
