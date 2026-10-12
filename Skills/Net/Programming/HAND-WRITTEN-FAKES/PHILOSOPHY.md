# HAND-WRITTEN-FAKES — Philosophy

A test double should be a small class that implements the interface directly. No mocking framework. No expression trees. No setup language. Just a class.

## The Core Belief

**A 1-method interface deserves a 3-line fake, not a 30-line mock setup.**

Mocking frameworks (Moq, NSubstitute, FakeItEasy) exist to compensate for *fat interfaces*. When your interface has fifteen methods, hand-rolling a fake for each test is painful, so you reach for a framework that lets you stub one method and silently no-op the others. The cost: every test becomes a half-page of `Setup(...).Returns(...)` and `Verify(...)` ceremony, the failure messages are stack traces inside the framework, and refactoring the interface breaks every test in subtle ways.

The right fix is not a better mocking framework. The right fix is **smaller interfaces**.

When interfaces are narrow — ideally one method, two at most — a hand-written fake is a 3-to-15-line class. It is faster to write than a mock setup, easier to read than a fluent assertion chain, and trivially debuggable because it is just code.

## Why Hand-Written Fakes

### 1. Debuggability

A fake is C#. Set a breakpoint inside it. Step through. Inspect the captured arguments in the watch window. Compare this to mock frameworks where the failure is a `MockException` thrown from a generated proxy with no source to inspect.

### 2. Refactor Safety

Rename a method on the interface. The fake fails to compile. Fix it. Done. With mocking frameworks, the test compiles fine and fails at runtime with `Setup() invoked on a method that does not exist`.

### 3. Reusability Across Tests

A well-named fake (`FakeReportWriter`, `FakeClock`, `FakeCoverageParser`) lives in a `Fakes/` folder and is shared across every test class in the project. Mock setups are bespoke per test method. Multiply that across hundreds of tests, and you have hundreds of redundant `Setup` chains.

### 4. Explicit State

A hand-written fake exposes its captured state as plain fields:

```csharp
internal sealed class FakeReportWriter : IReportWriter
{
    public QualityReport? LastReport   { get; private set; }
    public string?        LastOutputDir { get; private set; }
    public int            WriteCount    { get; private set; }

    public Task<string> WriteAsync(QualityReport report, string outputDir, CancellationToken ct = default)
    {
        LastReport   = report;
        LastOutputDir = outputDir;
        WriteCount++;
        return Task.FromResult(Path.Combine(outputDir, "fake-run"));
    }
}
```

The test reads `fake.LastReport` directly. No `Verify(x => x.WriteAsync(It.Is<...>(...)))` incantation.

### 5. No Dependency Tax

Every mocking framework you add is a dependency you must update, audit for CVEs, and explain to new contributors. Hand-written fakes use only what you already have: classes and interfaces.

## The Prerequisite: Interface Segregation

Hand-written fakes are **only** practical if interfaces are narrow. This is not a coincidence — it is the same discipline. The Interface Segregation Principle and the hand-written fake style reinforce each other:

- ISP says: prefer many small interfaces over few large ones.
- Hand-written fakes say: implementing a small interface is cheap.
- Together: design narrow seams, then fake them directly.

If your interface has 12 methods, you are not allowed to complain about hand-written fakes. Split the interface.

## When to Use Mocks Anyway (Spoiler: Almost Never)

There are two narrow cases where a mocking framework is acceptable:

1. **Sealed third-party classes you cannot wrap.** If a vendor exposes a sealed class with no interface and no virtual methods, a framework like `Microsoft.Fakes` or runtime weaving is your only option. The right answer is still to wrap it in your own interface and fake the wrapper.
2. **Verifying interaction order across many calls on a wide existing interface that you cannot refactor.** Even here, a hand-written fake with a `List<string> CallLog` field is usually clearer.

Neither case applies to interfaces you control. For your own interfaces, write a fake.

## What This Skill Replaces

This skill exists to let you delete:

- All `Moq` package references
- All `NSubstitute` package references
- All `FakeItEasy` package references
- All `using Moq;` lines
- All `Setup(x => ...).Returns(...)` chains
- All `mock.Verify(x => ..., Times.Once())` calls

In their place: a `Fakes/` folder with a handful of small, sharp, reusable test doubles.

## The Cultural Shift

Once you stop using mocking frameworks, three things happen:

1. **Tests get shorter.** Half the lines were mock setup; that half is gone.
2. **Tests get clearer.** Reading a fake's source tells you exactly what it does. Reading a `Setup` chain tells you what one mock did, but not what the system under test expected.
3. **Interfaces get smaller.** Without the crutch of "I'll just `Setup` the method I need," you start designing interfaces that have only the method you need.

The third effect is the most valuable. The mocking framework was hiding the design smell.
