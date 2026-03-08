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
