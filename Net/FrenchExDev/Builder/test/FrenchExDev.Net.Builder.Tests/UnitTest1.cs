using FrenchExDev.Net.Builder;
using FrenchExDev.Net.Result;
using Xunit;

namespace FrenchExDev.Net.Builder.Tests;

public class BuilderTests
{
    // ── AbstractBuilder<T> — core flow ────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_ReturnsFailure_WhenValidationContainsErrors()
    {
        var builder = new ValidationFailureBuilder();

        var result = await builder.BuildAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ValidationResult);
        Assert.Equal(0, builder.InstantiateCalls);
        // Kills line 147 statement-removal mutant: MemberNames must be populated
        var memberNames = result.ValidationResult!.MemberNames.ToList();
        Assert.Single(memberNames);
        // Kills line 147 string-format mutant: format must be "{FullType}.{PropName}"
        Assert.Equal(
            "FrenchExDev.Net.Builder.Tests.BuilderTests+ValidationFailureBuilder.Name",
            memberNames[0]);
    }

    [Fact]
    public async Task BuildAsync_IsSingleFlight_AcrossConcurrentCalls()
    {
        var builder = new ConcurrentBuilder();

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => builder.BuildAsync(cancellationToken: CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(1, builder.InstantiateCalls);

        var firstReference = results[0].ValueOrThrow();
        Assert.All(results, result => Assert.Same(firstReference, result.ValueOrThrow()));
        Assert.Equal("ready", firstReference.Resolved());
    }

    [Fact]
    public async Task BuildAsync_ReturnsCachedReference_OnSecondCall()
    {
        var builder = new ConcurrentBuilder();

        var first = await builder.BuildAsync();
        var second = await builder.BuildAsync();

        Assert.True(second.IsSuccess);
        Assert.Same(first.ValueOrThrow(), second.ValueOrThrow());
    }

    [Fact]
    public async Task BuildAsync_HandlesRecursiveVisit_WithoutReinstantiating()
    {
        var builder = new RecursiveBuilder();

        var result = await builder.BuildAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, builder.InstantiateCalls);
        Assert.Equal("recursive", result.ValueOrThrow().Resolved());
    }

    [Fact]
    public async Task BuildAsync_ReturnsFailure_WhenValidateAsyncItselfFails()
    {
        var builder = new ValidateAsyncResultFailureBuilder();

        var result = await builder.BuildAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("validate step failed", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public async Task BuildAsync_ReturnsFailure_WhenInstantiateReturnsFailure()
    {
        var builder = new InstantiateFailureBuilder();

        var result = await builder.BuildAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("instantiate failed", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public async Task BuildAsync_ReturnsFailure_WhenInstantiateReturnsDifferentUnresolvedReference()
    {
        var builder = new DifferentUnresolvedReferenceBuilder();

        var result = await builder.BuildAsync();

        Assert.False(result.IsSuccess);
        // Kills line 219 string mutation: error message must identify the contract violation
        Assert.Equal(
            "Instantiate must return the provided reference or a resolved reference.",
            result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public async Task BuildAsync_Succeeds_WhenInstantiateReturnsDifferentResolvedReference()
    {
        var builder = new DifferentResolvedReferenceBuilder();

        var result = await builder.BuildAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("from other ref", result.ValueOrThrow().Resolved());
    }

    // ── AbstractBuilder<T> — graph ────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_BuildsPersonChildGraph_WithParentBackReference()
    {
        var builder = new PersonBuilder("Alice")
            .AddChild("Bob")
            .AddChild("Claire");

        var result = await builder.BuildAsync();

        Assert.True(result.IsSuccess);

        var person = result.ValueOrThrow().Resolved();
        Assert.Equal("Alice", person.Name);
        Assert.Equal(2, person.Children.Count);
        Assert.Collection(
            person.Children,
            child => Assert.Equal("Bob", child.Name),
            child => Assert.Equal("Claire", child.Name));
        Assert.All(person.Children, child => Assert.Same(person, child.Parent));
    }

    // ── AbstractBuilder<T, TException> ───────────────────────────────────────

    [Fact]
    public async Task GenericBuildAsync_ReturnsFailure_OnValidationErrors()
    {
        var builder = new ExceptionalBuilder(hasValidationErrors: true);

        var result = await builder.BuildAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("validation failed", result.Error!.Message);
    }

    [Fact]
    public async Task GenericBuildAsync_ReturnsSuccess_WhenValidationPasses()
    {
        var builder = new ExceptionalBuilder(hasValidationErrors: false);

        var result = await builder.BuildAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("typed", result.Value);
    }

    [Fact]
    public void ExceptionalBuilder_BuildException_DelegatesToTypedBuildException()
    {
        var builder = new ExceptionalBuilder(hasValidationErrors: false);
        var resultVr = Result<ValidationResult>.Success(new ValidationResult());

        var ex = builder.InvokeBuildException(resultVr);

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("validation failed", ex.Message);
    }

    // ── Reference<T> ─────────────────────────────────────────────────────────

    [Fact]
    public void Reference_TryResolved_ReturnsFalse_WhenNotResolved()
    {
        var reference = new Reference<string>();

        var found = reference.TryResolved(out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Reference_Resolved_ThrowsReferenceNotResolvedException_WhenNotResolved()
    {
        var reference = new Reference<string>();

        var ex = Assert.Throws<ReferenceNotResolvedException>(() => reference.Resolved());
        // Kills line 73 string mutation
        Assert.Equal("Reference not resolved.", ex.Message);
    }

    [Fact]
    public void Reference_Resolve_ThrowsArgumentNullException_WhenValueIsNull()
    {
        var reference = new Reference<string>();

        Assert.Throws<ArgumentNullException>(() => reference.Resolve(null!));
    }

    [Fact]
    public void Reference_Resolve_ThrowsInvalidOperationException_WhenCalledTwice()
    {
        var reference = new Reference<string>();
        reference.Resolve("first");

        var ex = Assert.Throws<InvalidOperationException>(() => reference.Resolve("second"));
        // Kills line 43 string mutation
        Assert.Equal("Reference already resolved.", ex.Message);
    }

    // ── VisitedObjects ────────────────────────────────────────────────────────

    [Fact]
    public void VisitedObjects_IsVisited_ReturnsFalse_WhenObjectIsNull()
    {
        var visited = new VisitedObjects();
        var builder = new ConcurrentBuilder();

        Assert.False(visited.IsVisited(null, builder));
    }

    // ── ValidationResult ─────────────────────────────────────────────────────

    [Fact]
    public void ValidationResult_AddError_ThrowsArgumentNullException_WhenExceptionIsNull()
    {
        var vr = new ValidationResult();

        Assert.Throws<ArgumentNullException>(() =>
            vr.AddError(new MemberName("X", typeof(BuilderTests)), null!));
    }

    [Fact]
    public void ValidationResult_AddErrors_ByMember_AddsNonNullExceptions()
    {
        var vr = new ValidationResult();
        var member = new MemberName("X", typeof(BuilderTests));

        vr.AddErrors(member, new Exception[] { new InvalidOperationException("a"), null!, new ArgumentException("b") });

        var dr = vr.ToDataAnnotationsValidationResult();
        // Kills line 155 string mutation: "; " separator must be present between messages
        Assert.Equal("a; b", dr.ErrorMessage);
    }

    [Fact]
    public void ValidationResult_AddErrors_ByMember_ThrowsArgumentNullException_WhenEnumerableIsNull()
    {
        var vr = new ValidationResult();

        Assert.Throws<ArgumentNullException>(() =>
            vr.AddErrors(new MemberName("X", typeof(BuilderTests)), (IEnumerable<Exception>)null!));
    }

    [Fact]
    public void ValidationResult_AddErrors_ByTuples_AddsErrors()
    {
        var vr = new ValidationResult();
        var member = new MemberName("X", typeof(BuilderTests));

        vr.AddErrors(new[] { (member, (Exception)new InvalidOperationException("x")) });

        Assert.False(vr.IsSuccess);
        Assert.Equal("x", vr.ToDataAnnotationsValidationResult().ErrorMessage);
    }

    [Fact]
    public void ValidationResult_AddErrors_ByTuples_ThrowsArgumentNullException_WhenEnumerableIsNull()
    {
        var vr = new ValidationResult();

        Assert.Throws<ArgumentNullException>(() =>
            vr.AddErrors((IEnumerable<(MemberName, Exception)>)null!));
    }

    [Fact]
    public void ValidationResult_ToDataAnnotationsValidationResult_WhenNoErrors_ReturnsDefaultMessage()
    {
        var vr = new ValidationResult();

        var dr = vr.ToDataAnnotationsValidationResult();

        Assert.Equal("Validation failed.", dr.ErrorMessage);
    }

    [Fact]
    public void ValidationResult_ToDataAnnotationsValidationResult_WhenQueueEmpty_ReturnsDefaultMessage()
    {
        var vr = new ValidationResult();
        vr.AddErrors(new MemberName("X", typeof(BuilderTests)), Array.Empty<Exception>());

        var dr = vr.ToDataAnnotationsValidationResult();

        // Kills line 155 string mutation on "Validation failed." in the messages.Count==0 branch
        Assert.Equal("Validation failed.", dr.ErrorMessage);
    }

    // ── Helper domain types ───────────────────────────────────────────────────

    private sealed class Person
    {
        public Person(string name) { Name = name; }
        public string Name { get; }
        public List<Child> Children { get; } = new();
        public void AddChild(Child child) { Children.Add(child); }
    }

    private sealed class Child
    {
        public Child(string name) { Name = name; }
        public string Name { get; }
        public Person? Parent { get; private set; }
        public void SetParent(Person parent) { Parent = parent; }
    }

    // ── Helper builders ───────────────────────────────────────────────────────

    private sealed class ValidationFailureBuilder : AbstractBuilder<string>
    {
        private int _instantiateCalls;
        public int InstantiateCalls => _instantiateCalls;

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
        {
            Interlocked.Increment(ref _instantiateCalls);
            reference.Resolve("unreachable");
            return Task.FromResult(Result<Reference<string>>.Success(reference));
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
        {
            var vr = new ValidationResult();
            vr.AddError(new MemberName("Name", typeof(ValidationFailureBuilder)),
                new InvalidOperationException("invalid"));
            return Task.FromResult(Result<ValidationResult>.Success(vr));
        }
    }

    private sealed class ConcurrentBuilder : AbstractBuilder<string>
    {
        private int _instantiateCalls;
        public int InstantiateCalls => _instantiateCalls;

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override async Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
        {
            Interlocked.Increment(ref _instantiateCalls);
            await Task.Delay(50, ct);
            reference.Resolve("ready");
            return Result<Reference<string>>.Success(reference);
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class RecursiveBuilder : AbstractBuilder<string>
    {
        private int _instantiateCalls;
        public int InstantiateCalls => _instantiateCalls;

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override async Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
        {
            Interlocked.Increment(ref _instantiateCalls);
            var nested = await BuildAsync(visited, ct);
            if (!nested.IsSuccess) return nested;
            Assert.Same(reference, nested.ValueOrThrow());
            reference.Resolve("recursive");
            return Result<Reference<string>>.Success(reference);
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class ValidateAsyncResultFailureBuilder : AbstractBuilder<string>
    {
        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
            => throw new InvalidOperationException("Should not be called");

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult("validate step failed")));
    }

    private sealed class InstantiateFailureBuilder : AbstractBuilder<string>
    {
        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
            => Task.FromResult(Result<Reference<string>>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult("instantiate failed")));

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class DifferentUnresolvedReferenceBuilder : AbstractBuilder<string>
    {
        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
        {
            var different = new Reference<string>(); // not resolved
            return Task.FromResult(Result<Reference<string>>.Success(different));
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class DifferentResolvedReferenceBuilder : AbstractBuilder<string>
    {
        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
        {
            var different = new Reference<string>();
            different.Resolve("from other ref");
            return Task.FromResult(Result<Reference<string>>.Success(different));
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class PersonBuilder : AbstractBuilder<Person>
    {
        private readonly string _name;
        private readonly List<ChildBuilder> _childBuilders = new();

        public PersonBuilder(string name) { _name = name; }

        public PersonBuilder AddChild(string childName)
        {
            _childBuilders.Add(new ChildBuilder(childName, this));
            return this;
        }

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override async Task<Result<Reference<Person>>> Instantiate(
            Reference<Person> reference, VisitedObjects visited, CancellationToken ct)
        {
            var person = new Person(_name);
            reference.Resolve(person);

            foreach (var childBuilder in _childBuilders)
            {
                var childResult = await childBuilder.BuildAsync(visited, ct);
                if (!childResult.IsSuccess)
                    return Result<Reference<Person>>.Failure(
                        childResult.ValidationResult ??
                        new System.ComponentModel.DataAnnotations.ValidationResult("Child build failed."));
                person.AddChild(childResult.ValueOrThrow().Resolved());
            }

            return Result<Reference<Person>>.Success(reference);
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class ChildBuilder : AbstractBuilder<Child>
    {
        private readonly string _name;
        private readonly PersonBuilder _parentBuilder;

        public ChildBuilder(string name, PersonBuilder parentBuilder)
        { _name = name; _parentBuilder = parentBuilder; }

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override async Task<Result<Reference<Child>>> Instantiate(
            Reference<Child> reference, VisitedObjects visited, CancellationToken ct)
        {
            var child = new Child(_name);
            reference.Resolve(child);
            var parentResult = await _parentBuilder.BuildAsync(visited, ct);
            if (!parentResult.IsSuccess)
                return Result<Reference<Child>>.Failure(
                    parentResult.ValidationResult ??
                    new System.ComponentModel.DataAnnotations.ValidationResult("Parent build failed."));
            child.SetParent(parentResult.ValueOrThrow().Resolved());
            return Result<Reference<Child>>.Success(reference);
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));
    }

    private sealed class ExceptionalBuilder : AbstractBuilder<string, InvalidOperationException>
    {
        private readonly bool _hasValidationErrors;

        public ExceptionalBuilder(bool hasValidationErrors)
        { _hasValidationErrors = hasValidationErrors; }

        public Exception InvokeBuildException(Result<ValidationResult> vr) => BuildException(vr);

        protected override InvalidOperationException TypedBuildException(Result<ValidationResult> vr)
            => new InvalidOperationException("validation failed");

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visited, CancellationToken ct)
        {
            reference.Resolve("ignored");
            return Task.FromResult(Result<Reference<string>>.Success(reference));
        }

        protected override Task<Result<string, InvalidOperationException>> InstantiateAsync(
            CancellationToken ct)
            => Task.FromResult(Result<string, InvalidOperationException>.Success("typed"));

        protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
        {
            var vr = new ValidationResult();
            if (_hasValidationErrors)
                vr.AddError(new MemberName("Name", typeof(ExceptionalBuilder)),
                    new InvalidOperationException("invalid"));
            return Task.FromResult(Result<ValidationResult>.Success(vr));
        }
    }
}

// ── Reference ctor tests ─────────────────────────────────────────────────────

public class ReferenceCtorTests
{
    [Fact]
    public void Reference_ValueCtor_IsResolved()
    {
        var r = new Reference<string>("hello");
        Assert.True(r.IsResolved);
        Assert.Equal("hello", r.Resolved());
    }
}

// ── ReferenceList tests ──────────────────────────────────────────────────────

public class ReferenceListTests
{
    [Fact]
    public void Ctor_WithReferences_StoresAll()
    {
        var refs = new[] { Reference<string>.Resolved("a"), Reference<string>.Resolved("b") };
        var list = new ReferenceList<string>(refs);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void DefaultCtor_CreatesEmptyList()
    {
        var list = new ReferenceList<string>();
        Assert.Empty(list.AsEnumerable());
    }

    [Fact]
    public void AsEnumerable_ReturnsResolvedItems()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("a"),
            Reference<string>.Unresolved(),
            Reference<string>.Resolved("b")
        });
        var items = list.AsEnumerable().ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains("a", items);
        Assert.Contains("b", items);
    }

    [Fact]
    public void Contains_Instance_ReturnsTrueWhenPresent()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("x") });
        #pragma warning disable xUnit2017
        Assert.True(list.Contains("x"));
        Assert.False(list.Contains("y"));
        #pragma warning restore xUnit2017
    }

    [Fact]
    public void ElementAt_ReturnsResolvedValue()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("val") });
        Assert.Equal("val", list.ElementAt(0));
    }

    [Fact]
    public void Any_WithPredicate_ReturnsTrueWhenMatch()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("abc") });
        Assert.True(list.Any(s => s == "abc"));
        Assert.False(list.Any(s => s == "xyz"));
    }

    [Fact]
    public void Any_NoArgs_ReturnsTrueWhenResolved()
    {
        var empty = new ReferenceList<string>();
        Assert.False(empty.Any());

        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("a") });
        Assert.True(list.Any());
    }

    [Fact]
    public void All_ReturnsTrueWhenAllMatch()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("ab"),
            Reference<string>.Resolved("abc")
        });
        Assert.True(list.All(s => s.Length >= 2));
        Assert.False(list.All(s => s.Length >= 3));
    }

    [Fact]
    public void Where_FiltersResolved()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("short"),
            Reference<string>.Resolved("longer-string")
        });
        var filtered = list.Where(s => s.Length > 5).ToList();
        Assert.Single(filtered);
        Assert.Equal("longer-string", filtered[0]);
    }

    [Fact]
    public void Select_MapsResolved()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("abc") });
        var lengths = list.Select(s => s.Length).ToList();
        Assert.Single(lengths);
        Assert.Equal(3, lengths[0]);
    }

    [Fact]
    public void IndexOf_ReturnsCorrectIndex()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("a"),
            Reference<string>.Resolved("b")
        });
        Assert.Equal(1, list.IndexOf("b"));
    }

    [Fact]
    public void IndexOf_ThrowsWhenNotFound()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("a") });
        Assert.Throws<InvalidOperationException>(() => list.IndexOf("z"));
    }

    [Fact]
    public void CopyTo_CopiesResolvedItems()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("x"),
            Reference<string>.Resolved("y")
        });
        var arr = new string[3];
        list.CopyTo(arr, 1);
        Assert.Null(arr[0]);
        Assert.Equal("x", arr[1]);
        Assert.Equal("y", arr[2]);
    }

    [Fact]
    public void Remove_RemovesMatchingItem()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("a"),
            Reference<string>.Resolved("b")
        });
        Assert.True(list.Remove("a"));
        Assert.Single(list.AsEnumerable());
        Assert.False(list.Remove("z"));
    }

    [Fact]
    public void IsReadOnly_ReturnsFalse()
    {
        var list = new ReferenceList<string>();
        Assert.False(list.IsReadOnly);
    }

    [Fact]
    public void Indexer_GetSet_Works()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("old") });
        Assert.Equal("old", list[0]);
        list[0] = "new";
        Assert.Equal("new", list[0]);
    }

    [Fact]
    public void Add_Reference_AddsToList()
    {
        var list = new ReferenceList<string>();
        list.Add(Reference<string>.Resolved("added"));
        Assert.Single(list.AsEnumerable());
        Assert.Equal("added", list[0]);
    }

    [Fact]
    public void Add_Instance_AddsResolvedReference()
    {
        var list = new ReferenceList<string>();
        list.Add("direct");
        Assert.Single(list.AsEnumerable());
        Assert.Equal("direct", list[0]);
    }

    [Fact]
    public void Insert_InsertsAtIndex()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("a") });
        list.Insert(0, "b");
        Assert.Equal("b", list[0]);
        Assert.Equal("a", list[1]);
    }

    [Fact]
    public void RemoveAt_RemovesAtIndex()
    {
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("a"),
            Reference<string>.Resolved("b")
        });
        list.RemoveAt(0);
        Assert.Single(list.AsEnumerable().ToList());
        Assert.Equal("b", list[0]);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("a") });
        list.Clear();
        Assert.Empty(list.AsEnumerable());
    }

    [Fact]
    public void GetEnumerator_Enumerates()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("a") });
        var items = new List<string>();
        foreach (var item in list) items.Add(item);
        Assert.Single(items);
    }

    [Fact]
    public void Contains_Reference_Works()
    {
        var r = Reference<string>.Resolved("a");
        var list = new ReferenceList<string>(new[] { r });
        Assert.True(list.Contains(r));
        var other = Reference<string>.Resolved("a");
        // different Reference instance — Contains uses reference equality
        Assert.False(list.Contains(other));
    }

    [Fact]
    public void Queryable_Returns()
    {
        var list = new ReferenceList<string>(new[] { Reference<string>.Resolved("a") });
        var q = list.Queryable;
        Assert.Single(q);
    }

    // ── Mutation-killing tests ───────────────────────────────────────────────

    [Fact]
    public void All_ReturnsTrueForEmptyList()
    {
        // Kills L479 logical mutation: empty list → All returns true (vacuous truth)
        var list = new ReferenceList<string>();
        Assert.True(list.All(_ => false));
    }

    [Fact]
    public void All_ReturnsFalseWhenUnresolved()
    {
        // Kills L479 mutation: unresolved ref should cause All to return false
        var list = new ReferenceList<string>(new[] { Reference<string>.Unresolved() });
        Assert.False(list.All(_ => true));
    }

    [Fact]
    public void Where_SkipsUnresolved()
    {
        // Kills L493 logical mutation: unresolved refs must not appear in Where output
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("a"),
            Reference<string>.Unresolved()
        });
        var result = list.Where(_ => true).ToList();
        Assert.Single(result);
        Assert.Equal("a", result[0]);
    }

    [Fact]
    public void Select_SkipsUnresolved()
    {
        // Kills L507 logical mutation: unresolved refs must not appear in Select output
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("abc"),
            Reference<string>.Unresolved()
        });
        var result = list.Select(s => s.Length).ToList();
        Assert.Single(result);
        Assert.Equal(3, result[0]);
    }

    [Fact]
    public void IndexOf_ThrowsWithCorrectMessage()
    {
        // Kills L520 string mutation: error message must contain "Item not found"
        var list = new ReferenceList<string>();
        var ex = Assert.Throws<InvalidOperationException>(() => list.IndexOf("x"));
        Assert.Equal("Item not found", ex.Message);
    }

    [Fact]
    public void CopyTo_SkipsUnresolved()
    {
        // Kills L555 logical mutation: unresolved refs must not be copied
        var list = new ReferenceList<string>(new[]
        {
            Reference<string>.Resolved("a"),
            Reference<string>.Unresolved(),
            Reference<string>.Resolved("b")
        });
        var arr = new string[2];
        list.CopyTo(arr, 0);
        Assert.Equal("a", arr[0]);
        Assert.Equal("b", arr[1]);
    }
}

// ── BuilderList tests ────────────────────────────────────────────────────────

public class BuilderListTests
{
    [Fact]
    public void AsReferenceList_ReturnsReferences()
    {
        var list = new BuilderList<string, StringTestBuilder>();
        list.New(b => b.Value = "hello");
        var refs = list.AsReferenceList();
        #pragma warning disable xUnit2013
        Assert.Equal(1, refs.Count); // reference exists but not yet resolved — can't use Assert.Single (unresolved)
        #pragma warning restore xUnit2013
    }

    [Fact]
    public void New_AddsAndConfiguresBuilder()
    {
        var list = new BuilderList<string, StringTestBuilder>();
        var same = list.New(b => b.Value = "test");
        Assert.Same(list, same);
        Assert.Single(list);
        Assert.Equal("test", list[0].Value);
    }

    public class StringTestBuilder : AbstractBuilder<string>, IBuilder<string>
    {
        public string? Value { get; set; }

        protected override Task<Result<Reference<string>>> Instantiate(
            Reference<string> reference, VisitedObjects visitedObjects,
            CancellationToken cancellationToken = default)
        {
            reference.Resolve(Value ?? "");
            return Task.FromResult(Result<Reference<string>>.Success(reference));
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException();
    }
}

// ── DictionaryBuilder tests ──────────────────────────────────────────────────

public class DictionaryBuilderTests
{
    // ── 2-param DictionaryBuilder<TKey, TValue> ──────────────────────────────

    [Fact]
    public void Build_ReturnsEmptyDictionary_WhenNoEntriesAdded()
    {
        var db = new DictionaryBuilder<string, int>();
        var dict = db.Build();
        Assert.NotNull(dict);
        Assert.Empty(dict);
    }

    [Fact]
    public void With_AddsSingleEntry()
    {
        var dict = new DictionaryBuilder<string, int>()
            .With("a", 1)
            .Build();

        Assert.Single(dict);
        Assert.Equal(1, dict["a"]);
    }

    [Fact]
    public void With_AddsMultipleEntries()
    {
        var dict = new DictionaryBuilder<string, string>()
            .With("x", "hello")
            .With("y", "world")
            .Build();

        Assert.Equal(2, dict.Count);
        Assert.Equal("hello", dict["x"]);
        Assert.Equal("world", dict["y"]);
    }

    [Fact]
    public void With_OverwritesSameKey()
    {
        var dict = new DictionaryBuilder<string, int>()
            .With("a", 1)
            .With("a", 2)
            .Build();

        Assert.Single(dict);
        Assert.Equal(2, dict["a"]);
    }

    [Fact]
    public void With_ReturnsSameInstance_ForFluency()
    {
        var db = new DictionaryBuilder<int, string>();
        var same = db.With(1, "one");
        Assert.Same(db, same);
    }

    // ── 3-param DictionaryBuilder<TKey, TValue, TValueBuilder> ───────────────

    [Fact]
    public async Task ThreeParam_WithDirectValue_AddsToDictionary()
    {
        var db = new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>();
        var item = new SimpleItem("direct");
        db.With("k1", item);

        var dict = await db.BuildAsync(new VisitedObjects());
        Assert.Single(dict);
        Assert.Same(item, dict["k1"]);
    }

    [Fact]
    public async Task ThreeParam_WithFunc_AddsBuilderToDictionary()
    {
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .With("k1", b => b.WithName("built"))
            .BuildAsync(new VisitedObjects());

        Assert.Single(dict);
        Assert.Equal("built", dict["k1"].Name);
    }

    [Fact]
    public async Task ThreeParam_WithBuilder_AddsBuilderToDictionary()
    {
        var builder = new SimpleItemBuilder().WithName("pre-configured");
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .With("k1", builder)
            .BuildAsync(new VisitedObjects());

        Assert.Single(dict);
        Assert.Equal("pre-configured", dict["k1"].Name);
    }

    [Fact]
    public async Task ThreeParam_DirectValueOverridesBuilder()
    {
        var item = new SimpleItem("direct");
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .With("k1", b => b.WithName("from-builder"))
            .With("k1", item) // direct value overrides builder
            .BuildAsync(new VisitedObjects());

        Assert.Single(dict);
        Assert.Same(item, dict["k1"]);
    }

    [Fact]
    public async Task ThreeParam_BuilderOverridesDirectValue()
    {
        var item = new SimpleItem("direct");
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .With("k1", item)
            .With("k1", b => b.WithName("from-builder")) // builder overrides direct
            .BuildAsync(new VisitedObjects());

        Assert.Single(dict);
        Assert.Equal("from-builder", dict["k1"].Name);
    }

    [Fact]
    public async Task ThreeParam_MixedDirectAndBuilderEntries()
    {
        var item = new SimpleItem("direct");
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .With("a", item)
            .With("b", b => b.WithName("built"))
            .BuildAsync(new VisitedObjects());

        Assert.Equal(2, dict.Count);
        Assert.Same(item, dict["a"]);
        Assert.Equal("built", dict["b"].Name);
    }

    [Fact]
    public async Task ThreeParam_BuildAsync_PassesVisitedObjects()
    {
        var visited = new VisitedObjects();
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .With("k1", b => b.WithName("test"))
            .BuildAsync(visited);

        Assert.Single(dict);
        Assert.Equal("test", dict["k1"].Name);
    }

    [Fact]
    public void ThreeParam_WithFunc_ReturnsFluently()
    {
        var db = new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>();
        var same = db.With("k1", b => b.WithName("x"));
        Assert.Same(db, same);
    }

    [Fact]
    public void ThreeParam_WithBuilder_ReturnsFluently()
    {
        var db = new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>();
        var same = db.With("k1", new SimpleItemBuilder());
        Assert.Same(db, same);
    }

    [Fact]
    public void ThreeParam_WithDirectValue_ReturnsFluently()
    {
        var db = new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>();
        var same = db.With("k1", new SimpleItem("x"));
        Assert.Same(db, same);
    }

    [Fact]
    public async Task ThreeParam_EmptyBuilder_ReturnsEmptyDictionary()
    {
        var dict = await new DictionaryBuilder<string, SimpleItem, SimpleItemBuilder>()
            .BuildAsync(new VisitedObjects());
        Assert.NotNull(dict);
        Assert.Empty(dict);
    }

    // ── Test helpers ─────────────────────────────────────────────────────────

    public sealed class SimpleItem
    {
        public SimpleItem(string name) => Name = name;
        public string Name { get; }
    }

    public sealed class SimpleItemBuilder : AbstractBuilder<SimpleItem>
    {
        private string? _name;

        public SimpleItemBuilder WithName(string name) { _name = name; return this; }

        protected override Task<Result<Reference<SimpleItem>>> Instantiate(
            Reference<SimpleItem> reference, VisitedObjects visitedObjects,
            CancellationToken cancellationToken = default)
        {
            reference.Resolve(new SimpleItem(_name ?? "default"));
            return Task.FromResult(Result<Reference<SimpleItem>>.Success(reference));
        }

        protected override Task<Result<ValidationResult>> ValidateAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));

        protected override Exception BuildException(Result<ValidationResult> vr)
            => new InvalidOperationException(vr.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);
    }
}
