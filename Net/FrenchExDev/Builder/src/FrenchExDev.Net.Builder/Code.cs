using FrenchExDev.Net.Result;
using System.Collections;
using System.Collections.Concurrent;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace FrenchExDev.Net.Builder;

public interface IBuilder
{

}

public class ReferenceNotResolvedException : Exception
{
    public ReferenceNotResolvedException(string message) : base(message)
    {
    }
}

public sealed record Reference<TClass> where TClass : notnull
{
    private readonly object _sync = new();
    private bool _isResolved;
    private TClass? _referenced;

    public Reference() { }
    public Reference(TClass reference) { _referenced = reference; _isResolved = true; }

    public static Reference<TClass> Unresolved() => new();
    public static Reference<TClass> Resolved(TClass reference) => new(reference);

    public bool IsResolved
    {
        get
        {
            lock (_sync)
            {
                return _isResolved;
            }
        }
    }

    public Reference<TClass> Resolve(TClass referenced)
    {
        if (referenced is null) throw new ArgumentNullException(nameof(referenced));

        lock (_sync)
        {
            if (_isResolved)
            {
                throw new InvalidOperationException("Reference already resolved.");
            }

            _referenced = referenced;
            _isResolved = true;
        }

        return this;
    }

    public bool TryResolved(out TClass referenced)
    {
        lock (_sync)
        {
            if (!_isResolved)
            {
                referenced = default!;
                return false;
            }

            referenced = _referenced!;
            return true;
        }
    }

    public TClass Resolved()
    {
        if (TryResolved(out var referenced))
        {
            return referenced;
        }

        throw new ReferenceNotResolvedException("Reference not resolved.");
    }
}

public sealed class VisitedObjects
{
    private readonly ConcurrentDictionary<object, IBuilder> _visited = new(ReferenceEqualityComparer.Instance);

    public bool IsVisited(object? obj, IBuilder builder)
    {
        if (obj is null)
        {
            return false;
        }

        return !_visited.TryAdd(obj, builder);
    }
}

public record MemberName(string Name, Type DeclaringType);

public sealed class ValidationResult
{
    private readonly ConcurrentDictionary<MemberName, ConcurrentQueue<Exception>> _errors = new();

    public bool IsSuccess => _errors.IsEmpty;

    public void AddError(MemberName memberName, Exception exception)
    {
        if (exception is null) throw new ArgumentNullException(nameof(exception));

        var exceptions = _errors.GetOrAdd(memberName, static _ => new ConcurrentQueue<Exception>());
        exceptions.Enqueue(exception);
    }

    public void AddErrors(MemberName memberName, IEnumerable<Exception> exceptions)
    {
        if (exceptions is null) throw new ArgumentNullException(nameof(exceptions));

        var queuedExceptions = _errors.GetOrAdd(memberName, static _ => new ConcurrentQueue<Exception>());

        foreach (var exception in exceptions)
        {
            if (exception is null)
            {
                continue;
            }

            queuedExceptions.Enqueue(exception);
        }
    }

    public void AddErrors(IEnumerable<(MemberName memberName, Exception exception)> errors)
    {
        if (errors is null) throw new ArgumentNullException(nameof(errors));

        foreach (var (memberName, exception) in errors)
        {
            AddError(memberName, exception);
        }
    }

    public DataAnnotationsValidationResult ToDataAnnotationsValidationResult()
    {
        if (IsSuccess)
        {
            return new DataAnnotationsValidationResult("Validation failed.");
        }

        var memberNames = new HashSet<string>(StringComparer.Ordinal);
        var messages = new List<string>();

        foreach (var __kvp in _errors)
        {
            memberNames.Add($"{__kvp.Key.DeclaringType.FullName}.{__kvp.Key.Name}");

            foreach (var exception in __kvp.Value)
            {
                messages.Add(exception.Message);
            }
        }

        var message = messages.Count == 0 ? "Validation failed." : string.Join("; ", messages);
        return new DataAnnotationsValidationResult(message, memberNames);
    }
}

public interface IBuilder<TClass> : IBuilder where TClass : notnull
{
    Task<Result<Reference<TClass>>> BuildAsync(VisitedObjects? visitedObjects = null, CancellationToken cancellationToken = default);
    Reference<TClass> Reference();
}

public interface IBuilder<TResult, TError> : IBuilder where TResult : notnull where TError : Exception
{
    Task<Result<TResult, TError>> BuildAsync(CancellationToken cancellationToken = default);
    Reference<Result<TResult, TError>> Reference();
}

public abstract class AbstractBuilder<TClass> : IBuilder<TClass> where TClass : notnull
{
    private readonly Reference<TClass> _reference = new();
    private readonly SemaphoreSlim _buildLock = new(1, 1);

    public Reference<TClass> Reference() => _reference;

    public virtual async Task<Result<Reference<TClass>>> BuildAsync(VisitedObjects? visitedObjects = null, CancellationToken cancellationToken = default)
    {
        visitedObjects ??= new VisitedObjects();

        if (visitedObjects.IsVisited(this, this))
            return Result<Reference<TClass>>.Success(_reference);

        // Stryker disable once Block : equivalent mutant
        if (_reference.IsResolved)
            return Result<Reference<TClass>>.Success(_reference);

        return await BuildAsyncLocked(visitedObjects, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Reference<TClass>>> BuildAsyncLocked(VisitedObjects visitedObjects, CancellationToken cancellationToken)
    {
        await _buildLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_reference.IsResolved)
                return Result<Reference<TClass>>.Success(_reference);

            var validationResult = await ValidateAsync(cancellationToken).ConfigureAwait(false);

            if (HasValidationErrors(validationResult))
                return Result<Reference<TClass>>.Failure(ToFailureValidationResult(validationResult));

            return await InstantiateAndResolve(visitedObjects, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _buildLock.Release();
        }
    }

    private async Task<Result<Reference<TClass>>> InstantiateAndResolve(VisitedObjects visitedObjects, CancellationToken cancellationToken)
    {
        var instantiateResult = await Instantiate(_reference, visitedObjects, cancellationToken).ConfigureAwait(false);

        if (!instantiateResult.IsSuccess)
            return instantiateResult;

        var builtReference = instantiateResult.ValueOrThrow();

        if (ReferenceEquals(builtReference, _reference))
            return instantiateResult;

        if (!builtReference.TryResolved(out var referenced))
        {
            return Result<Reference<TClass>>.Failure(
                new DataAnnotationsValidationResult("Instantiate must return the provided reference or a resolved reference."));
        }

        _reference.Resolve(referenced);
        return Result<Reference<TClass>>.Success(_reference);
    }

    protected static bool HasValidationErrors(Result<ValidationResult> validationResult)
    {
        if (!validationResult.IsSuccess)
        {
            return true;
        }

        return !validationResult.ValueOrThrow().IsSuccess;
    }

    protected static DataAnnotationsValidationResult ToFailureValidationResult(Result<ValidationResult> validationResult)
    {
        if (!validationResult.IsSuccess)
        {
            return validationResult.ValidationResult!;
        }

        return validationResult.ValueOrThrow().ToDataAnnotationsValidationResult();
    }

    protected abstract Exception BuildException(Result<ValidationResult> validationResult);

    protected abstract Task<Result<Reference<TClass>>> Instantiate(
        Reference<TClass> reference,
        VisitedObjects visitedObjects,
        CancellationToken cancellationToken = default);

    protected abstract Task<Result<ValidationResult>> ValidateAsync(CancellationToken cancellationToken = default);
}

public abstract class AbstractBuilder<TClass, TException> :
    AbstractBuilder<TClass>,
    IBuilder<TClass, TException>
    where TClass : notnull
    where TException : Exception
{
    public virtual async Task<Result<TClass, TException>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateAsync(cancellationToken).ConfigureAwait(false);

        if (HasValidationErrors(validationResult))
        {
            return Result<TClass, TException>.Failure(TypedBuildException(validationResult));
        }

        return await InstantiateAsync(cancellationToken).ConfigureAwait(false);
    }

    protected sealed override Exception BuildException(Result<ValidationResult> validationResult) => TypedBuildException(validationResult);

    protected abstract TException TypedBuildException(Result<ValidationResult> validationResult);

    protected abstract Task<Result<TClass, TException>> InstantiateAsync(CancellationToken cancellationToken = default);

    Reference<Result<TClass, TException>> IBuilder<TClass, TException>.Reference()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Represents a list of object references that supports enumeration, querying, and reference-based operations.
/// </summary>
/// <remarks>This interface extends <see cref="IList{TClass}"/> to provide additional methods for working with
/// references, including LINQ-style querying and reference containment checks. It is commonly used in scenarios where
/// objects are managed or tracked by reference rather than by value.</remarks>
/// <typeparam name="TClass">The type of class objects referenced by the list. Must be a reference type.</typeparam>
public interface IReferenceList<TClass> : IList<TClass> where TClass : class
{
    /// <summary>
    /// Returns an enumerable collection of resolved instances of type <typeparamref name="TClass"/> contained in the
    /// </summary>
    /// <returns></returns>
    IEnumerable<TClass> AsEnumerable();

    /// <summary>
    /// Gets the queryable collection of entities of type <typeparamref name="TClass"/> for LINQ operations.
    /// </summary>
    /// <remarks>Use this property to construct LINQ queries against the underlying data source. The returned
    /// <see cref="IQueryable{TClass}"/> supports deferred execution and can be used to filter, sort, and project
    /// entities before materializing results.</remarks>
    IQueryable<TClass> Queryable { get; }

    /// <summary>
    /// Returns the element at the specified zero-based index in the collection.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve. Must be greater than or equal to 0 and less than the total
    /// number of elements in the collection.</param>
    /// <returns>The element of type TClass at the specified index.</returns>
    TClass ElementAt(int index); bool Any(Func<TClass, bool> value); bool Any(); void Add(Reference<TClass> reference); bool Contains(Reference<TClass> reference);
}

public class FailuresDictionary : Dictionary<MemberName, List<Exception>>
{
    public FailuresDictionary() : base() { }

    public FailuresDictionary(IDictionary<MemberName, List<Exception>> dictionary) : base(dictionary) { }

    public FailuresDictionary(IEqualityComparer<MemberName> comparer) : base(comparer) { }

    public FailuresDictionary(int capacity) : base(capacity) { }

    public FailuresDictionary(IDictionary<MemberName, List<Exception>> dictionary, IEqualityComparer<MemberName> comparer) : base(dictionary, comparer) { }

    public FailuresDictionary(int capacity, IEqualityComparer<MemberName> comparer) : base(capacity, comparer) { }
}

public class NotResolvedException : Exception { public NotResolvedException(string message) : base(message) { } }


/// <summary>
/// Implements a list of references to objects of type <typeparamref name="TClass"/>, providing methods for adding,
/// </summary>
/// <typeparam name="TClass"></typeparam>
public class ReferenceList<TClass> : IReferenceList<TClass> where TClass : class
{
    /// <summary>
    /// List of references to objects of type <typeparamref name="TClass"/>.
    /// </summary>
    private readonly List<Reference<TClass>> _references;

    /// <summary>
    /// Initializes a new instance of the ReferenceList class with the specified collection of references.
    /// </summary>
    /// <param name="references">An enumerable collection of Reference<TClass> objects to include in the list. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if references is null.</exception>
    public ReferenceList(IEnumerable<Reference<TClass>> references)
    {
        // Stryker disable once NullCoalescing : defensive guard — ToList() never returns null
        _references = references.ToList() ?? throw new ArgumentNullException(nameof(references));
    }

    /// <summary>
    /// Returns an enumerable collection of all resolved instances referenced by this object.
    /// </summary>
    /// <remarks>Only references that are both resolved and have a non-null instance are included in the
    /// returned collection. The enumeration reflects the current state of the references at the time of the
    /// call.</remarks>
    /// <returns>An <see cref="IEnumerable{TClass}"/> containing each resolved instance. The collection will be empty if no
    /// references are resolved.</returns>
    public IEnumerable<TClass> AsEnumerable()
    {
        foreach (var r in _references) if (r.IsResolved) yield return r.Resolved();
    }

    /// <summary>
    /// Initializes a new instance of the ReferenceList class.
    /// </summary>
    public ReferenceList() { _references = []; }

    /// <summary>
    /// Adds the specified reference to the collection.
    /// </summary>
    /// <param name="reference">The reference to add. Cannot be null.</param>
    public void Add(Reference<TClass> reference) => _references.Add(reference);

    /// <summary>
    /// Adds a reference to the specified instance of type TClass to the collection.
    /// </summary>
    /// <param name="instance">The instance of type TClass to be referenced and added. Cannot be null.</param>
    public void Add(TClass instance) => _references.Add(new Reference<TClass>().Resolve(instance));

    /// <summary>
    /// Determines whether the collection contains the specified reference.
    /// </summary>
    /// <param name="reference">The reference to locate in the collection. Cannot be null.</param>
    /// <returns>true if the specified reference is found in the collection; otherwise, false.</returns>
    public bool Contains(Reference<TClass> reference) => _references.Contains(reference);

    /// <summary>
    /// Determines whether the specified instance is present among the resolved references.
    /// </summary>
    /// <param name="instance">The object to locate within the collection of resolved references. Can be null if null references are supported.</param>
    /// <returns>true if the specified instance is found among the resolved references; otherwise, false.</returns>
    public bool Contains(TClass instance)
    {
        foreach (var r in _references) if (r.IsResolved && r.Resolved() == instance) return true; return false;
    }

    /// <summary>
    /// Gets an <see cref="IQueryable{TClass}"/> that enables LINQ queries over the collection of <typeparamref
    /// name="TClass"/> entities.
    /// </summary>
    /// <remarks>The returned <see cref="IQueryable{TClass}"/> supports deferred execution and can be used to
    /// compose LINQ queries. Changes made to the underlying collection after obtaining the queryable may not be
    /// reflected in previously constructed queries.</remarks>
    public IQueryable<TClass> Queryable => AsEnumerable().AsQueryable();

    /// <summary>
    /// Returns the resolved element at the specified index in the collection.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve. Must be within the bounds of the collection.</param>
    /// <returns>The resolved element of type TClass at the specified index.</returns>
    /// <exception cref="NotResolvedException">Thrown if the element at the specified index cannot be resolved.</exception>
    // Stryker disable once NullCoalescing : defensive guard — Resolved() never returns null for notnull TClass
    public TClass ElementAt(int index) => _references[index].Resolved() ?? throw new NotResolvedException(nameof(_references));

    /// <summary>
    /// Determines whether any resolved reference satisfies the specified predicate.
    /// </summary>
    /// <remarks>Only references that are resolved and have a non-null instance are considered. The predicate
    /// is not invoked for unresolved or null instances.</remarks>
    /// <param name="value">A function that defines the condition to test for each resolved instance. The function receives a non-null
    /// instance of type TClass and should return <see langword="true"/> to indicate a match.</param>
    /// <returns>true if at least one resolved reference matches the predicate; otherwise, false.</returns>
    public bool Any(Func<TClass, bool> value)
    {
        foreach (var r in _references) if (r.IsResolved && value(r.Resolved())) return true; return false;
    }

    /// <summary>
    /// Determines whether any referenced item is both resolved and has a non-null instance.
    /// </summary>
    /// <returns>true if at least one reference is resolved and its instance is not null; otherwise, false.</returns>
    public bool Any()
    {
        foreach (var r in _references) if (r.IsResolved) return true; return false;
    }

    /// <summary>
    /// Determines whether all resolved references satisfy the specified predicate.
    /// </summary>
    /// <remarks>Only references that are resolved and have a non-null instance are evaluated. Unresolved
    /// references or references with null instances are considered as not satisfying the condition.</remarks>
    /// <param name="value">A function that defines the condition to test for each resolved instance. The function receives a resolved
    /// instance of type TClass and should return <see langword="true"/> if the condition is met; otherwise, <see
    /// langword="false"/>.</param>
    /// <returns>true if every resolved reference has a non-null instance and the predicate returns true for each instance;
    /// otherwise, false.</returns>
    public bool All(Func<TClass, bool> value)
    {
        foreach (var r in _references) if (!(r.IsResolved && r.Resolved() is not null && value(r.Resolved()))) return false; return true;
    }

    /// <summary>
    /// Returns an enumerable collection of resolved instances that satisfy the specified predicate.
    /// </summary>
    /// <remarks>Only instances that are both resolved and not <see langword="null"/> are considered. The
    /// returned sequence is evaluated lazily.</remarks>
    /// <param name="predicate">A function to test each resolved instance for a condition. The function should return <see langword="true"/> to
    /// include the instance in the result; otherwise, <see langword="false"/>.</param>
    /// <returns>An <see cref="IEnumerable{TClass}"/> containing the resolved instances for which <paramref name="predicate"/>
    /// returns <see langword="true"/>. The collection is empty if no instances match the condition.</returns>
    public IEnumerable<TClass> Where(Func<TClass, bool> predicate)
    {
        foreach (var r in _references) if (r.IsResolved && r.Resolved() is not null && predicate(r.Resolved())) yield return r.Resolved();
    }

    /// <summary>
    /// Projects each resolved reference in the collection into a new form by applying the specified mapping function.
    /// </summary>
    /// <remarks>Only references that are resolved and have a non-null instance are included in the result.
    /// The mapping function is applied to each such instance in the collection.</remarks>
    /// <typeparam name="TOtherClass">The type of the value returned by the mapping function.</typeparam>
    /// <param name="mapper">A function that transforms each resolved instance of type TClass into a value of type TOtherClass. Cannot be
    /// null.</param>
    /// <returns>An enumerable collection of mapped values of type TOtherClass, corresponding to each resolved reference.</returns>
    public IEnumerable<TOtherClass> Select<TOtherClass>(Func<TClass, TOtherClass> mapper)
    {
        foreach (var r in _references) if (r.IsResolved && r.Resolved() is not null) yield return mapper(r.Resolved());
    }

    /// <summary>
    /// Returns the zero-based index of the first resolved reference whose instance matches the specified item.
    /// </summary>
    /// <param name="item">The object to locate in the collection. The search is performed among resolved references whose instance equals
    /// this item.</param>
    /// <returns>The zero-based index of the first occurrence of the specified item among resolved references.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified item is not found among resolved references.</exception>
    public int IndexOf(TClass item)
    {
        for (int i = 0; i < _references.Count; i++) { var r = _references[i]; if (r.IsResolved && r.Resolved() == item) return i; }
        throw new InvalidOperationException("Item not found");
    }

    /// <summary>
    /// Inserts the specified item into the collection at the given index.
    /// </summary>
    /// <param name="index">The zero-based index at which the item should be inserted. Must be greater than or equal to 0 and less than or
    /// equal to the number of items in the collection.</param>
    /// <param name="item">The item to insert into the collection.</param>
    public void Insert(int index, TClass item) => _references.Insert(index, new Reference<TClass>().Resolve(item));

    /// <summary>
    /// Removes the element at the specified index from the collection.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove. Must be greater than or equal to 0 and less than the number of
    /// elements in the collection.</param>
    public void RemoveAt(int index) => _references.RemoveAt(index);

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    public void Clear() => _references.Clear();

    /// <summary>
    /// Copies the resolved instances contained in the collection to the specified array, starting at the specified
    /// array index.
    /// </summary>
    /// <remarks>Only instances that are resolved and not null are copied. The method does not resize the
    /// destination array or skip indices; ensure the array has enough capacity to hold all resolved instances starting
    /// at the specified index.</remarks>
    /// <param name="array">The destination array to which the resolved instances will be copied. Must not be null and must have sufficient
    /// space to accommodate the copied elements.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins. Must be within the bounds of the array.</param>
    public void CopyTo(TClass[] array, int arrayIndex)
    {
        var i = arrayIndex; foreach (var r in _references) if (r.IsResolved && r.Resolved() is not null) array[i++] = r.Resolved();
    }

    /// <summary>
    /// Removes the specified item from the collection if it is present and resolved.
    /// </summary>
    /// <param name="item">The item to remove from the collection. Only items that are currently resolved will be considered for removal.</param>
    /// <returns>true if the item was found and removed; otherwise, false.</returns>
    public bool Remove(TClass item)
    {
        for (int i = 0; i < _references.Count; i++) { var r = _references[i]; if (r.IsResolved && r.Resolved() == item) { _references.RemoveAt(i); return true; } }
        return false;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection of <typeparamref name="TClass"/> objects.</returns>
    public IEnumerator<TClass> GetEnumerator() => AsEnumerable().GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the number of references contained in the collection.
    /// </summary>
    public int Count => _references.Count;

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the resolved reference at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the reference to retrieve or assign. Must be within the bounds of the collection.</param>
    /// <returns>The resolved reference of type TClass at the specified index.</returns>
    public TClass this[int index]
    {
        get => _references[index].Resolved();
        set => _references[index] = new Reference<TClass>().Resolve(value);
    }
}

/// <summary>
/// Represents a collection of builder objects that can construct instances of a specified class type. Provides methods
/// to build, validate, and convert builders to reference lists.
/// </summary>
/// <remarks>Use this class to manage a set of builders for batch construction, validation, or reference
/// conversion of objects of type <typeparamref name="TClass"/>. The collection supports adding new builders, building
/// all objects, and retrieving validation or build failures for each builder.</remarks>
/// <typeparam name="TClass">The type of class instances to be constructed by the builders. Must be a reference type.</typeparam>
/// <typeparam name="TBuilder">The type of builder used to construct instances of <typeparamref name="TClass"/>. Must implement <see
/// cref="IBuilder{TClass}"/> and have a parameterless constructor.</typeparam>
public class BuilderList<TClass, TBuilder> : List<TBuilder>
where TClass : class
where TBuilder : IBuilder<TClass>, new()
{
    /// <summary>
    /// Creates a new reference list containing references to each item in the collection.
    /// </summary>
    /// <returns>A <see cref="ReferenceList{TClass}"/> containing references to the items in the current collection.</returns>
    public ReferenceList<TClass> AsReferenceList()
    {
        var references = this.Select(x => x.Reference());
        return new(references);
    }

    /// <summary>
    /// Creates a new builder instance, applies the specified configuration action, and adds the builder to the list.
    /// </summary>
    /// <param name="body">An action that configures the newly created builder instance before it is added to the list. Cannot be null.</param>
    /// <returns>The current list containing the newly added and configured builder instance.</returns>
    public BuilderList<TClass, TBuilder> New(Action<TBuilder> body)
    {
        var builder = new TBuilder(); body(builder); Add(builder); return this;
    }
}

/// <summary>
/// Fluent dictionary builder for properties typed as <c>Dictionary&lt;TKey, TValue&gt;</c>.
/// Used by generated builder lambda overloads.
/// </summary>
public class DictionaryBuilder<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dict = new();

    public DictionaryBuilder<TKey, TValue> With(TKey key, TValue value)
    {
        _dict[key] = value;
        return this;
    }

    public Dictionary<TKey, TValue> Build() => _dict;
}

/// <summary>
/// Fluent dictionary builder for properties typed as <c>Dictionary&lt;TKey, TValue&gt;</c>
/// where the value type has a known builder extending <see cref="AbstractBuilder{TClass}"/>.
/// Stores value builders for deferred building during the parent's <c>Instantiate</c> phase.
/// </summary>
public class DictionaryBuilder<TKey, TValue, TValueBuilder>
    where TKey : notnull
    where TValue : notnull
    where TValueBuilder : AbstractBuilder<TValue>, new()
{
    private readonly Dictionary<TKey, TValue> _values = new();
    private readonly Dictionary<TKey, TValueBuilder> _builders = new();

    /// <summary>Adds a direct (pre-built) value.</summary>
    public DictionaryBuilder<TKey, TValue, TValueBuilder> With(TKey key, TValue value)
    {
        _builders.Remove(key);
        _values[key] = value;
        return this;
    }

    /// <summary>Adds a value via a fluent builder configuration lambda.</summary>
    public DictionaryBuilder<TKey, TValue, TValueBuilder> With(
        TKey key, Func<TValueBuilder, TValueBuilder> configure)
    {
        // Stryker disable once Statement : equivalent mutant — _builders loop overwrites _values in BuildAsync
        _values.Remove(key);
        _builders[key] = configure(new TValueBuilder());
        return this;
    }

    /// <summary>Adds a pre-configured builder instance.</summary>
    public DictionaryBuilder<TKey, TValue, TValueBuilder> With(TKey key, TValueBuilder builder)
    {
        // Stryker disable once Statement : equivalent mutant — _builders loop overwrites _values in BuildAsync
        _values.Remove(key);
        _builders[key] = builder;
        return this;
    }

    /// <summary>
    /// Builds all stored builders and merges with direct values.
    /// Called during the parent builder's <c>Instantiate</c> phase.
    /// </summary>
    public async Task<Dictionary<TKey, TValue>> BuildAsync(
        VisitedObjects visitedObjects, CancellationToken cancellationToken = default)
    {
        var dict = new Dictionary<TKey, TValue>(_values);
        foreach (var kvp in _builders)
        {
            var result = await kvp.Value.BuildAsync(visitedObjects, cancellationToken);
            dict[kvp.Key] = result.ValueOrThrow().Resolved();
        }
        return dict;
    }
}

/// <summary>
/// Fluent list builder for collection properties where the item type has a known builder
/// extending <see cref="AbstractBuilder{TClass}"/>.
/// Stores item builders for deferred building during the parent's <c>Instantiate</c> phase.
/// </summary>
public class ListBuilder<T, TBuilder>
    where T : notnull
    where TBuilder : AbstractBuilder<T>, new()
{
    private readonly List<T> _values = new();
    private readonly List<TBuilder> _builders = new();

    /// <summary>Adds a direct (pre-built) value.</summary>
    public ListBuilder<T, TBuilder> Add(T value)
    {
        _values.Add(value);
        return this;
    }

    /// <summary>Adds a value via a builder configuration action.</summary>
    public ListBuilder<T, TBuilder> Add(Action<TBuilder> configure)
    {
        var builder = new TBuilder();
        configure(builder);
        _builders.Add(builder);
        return this;
    }

    /// <summary>Adds a pre-configured builder instance.</summary>
    public ListBuilder<T, TBuilder> Add(TBuilder builder)
    {
        _builders.Add(builder);
        return this;
    }

    /// <summary>
    /// Builds all stored builders and merges with direct values.
    /// Called during the parent builder's <c>Instantiate</c> phase.
    /// </summary>
    public async Task<List<T>> BuildAsync(
        VisitedObjects visitedObjects, CancellationToken cancellationToken = default)
    {
        var list = new List<T>(_values);
        foreach (var b in _builders)
        {
            var result = await b.BuildAsync(visitedObjects, cancellationToken);
            list.Add(result.ValueOrThrow().Resolved());
        }
        return list;
    }
}