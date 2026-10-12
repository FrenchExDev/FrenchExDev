# QUALITY-GATES — How-To

## Adding a New Analyzer

1. **Create a static class** in `QualityGate/src/FrenchExDev.Net.QualityGate/Analysis/`:
   ```csharp
   internal static class MyNewAnalyzer
   {
       public static MyMetricResult Analyze(Compilation compilation)
       {
           // Pure function: walk syntax/symbols, compute metrics, return result
       }
   }
   ```

2. **Add the result type** to `QualityGate/src/.../Model/` if the metric doesn't fit existing model types.

3. **Call from `ProjectAnalyzer`** — add the analyzer call alongside existing analyzers in the per-project analysis flow.

4. **Add gate evaluation** in `QualityGate/src/.../Gates/QualityGateEvaluator.cs`:
   ```csharp
   if (config.MaxMyMetric is { } threshold && report.MyMetric > threshold)
       failures.Add(new GateFailure("my-metric", report.MyMetric, threshold));
   ```

5. **Write tests** using `RoslynTestHelper.CreateProject()`:
   ```csharp
   [Fact]
   public void Detects_high_metric_value()
   {
       var project = RoslynTestHelper.CreateProject(@"
           public class Bad { /* code that triggers high metric */ }
       ");
       var compilation = project.GetCompilationAsync().Result!;
       var result = MyNewAnalyzer.Analyze(compilation);
       result.Value.ShouldBeGreaterThan(expected);
   }
   ```

Reference: `QualityGate/src/.../Analysis/ComplexityAnalyzer.cs`

## Adding a New Quality Gate

1. **Add threshold key** to the config model and `quality-gate.yml`:
   ```yaml
   gates:
     max-my-metric: 10
   ```

2. **Add evaluation logic** in `QualityGateEvaluator`.

3. **Test both pass and fail scenarios** — create compilations that pass and fail the threshold.

4. **Update `QUALITY-GATE.md`** documentation with the new gate description.

## Running Quality Gates

```bash
# Full workflow: tests + coverage + analysis
dotnet quality-gate test

# Analysis only (uses existing coverage data)
dotnet quality-gate analyze

# CI/CD gate (exit code 1 on failure)
dotnet quality-gate check

# Serve HTML report in browser
dotnet quality-gate test --serve

# Print interface-to-implementation mapping
dotnet quality-gate interfaces
```

## Testing Without MSBuild

Use `RoslynTestHelper` + Fakes to test without MSBuild or filesystem:

```csharp
var engine = new QualityEngine(
    config,
    solutionLoader: FakeSolutionLoader.WithSource(@"
        public class MyClass { public int Prop { get; set; } }
    "),
    coverageParser: new FakeCoverageParser(null),
    mutationParser: new FakeMutationParser(null),
    reportWriter: new FakeReportWriter()
);

var report = await engine.AnalyzeAsync();
```

See: `QualityGate/test/.../QualityEngineWithFakesTests.cs`
