# SOLID — How-To

Step-by-step instructions for adding new instances of SOLID patterns in this codebase.

## Adding a New Infrastructure Seam

When you need to abstract an infrastructure dependency (filesystem, HTTP, database, external tool):

1. **Define a 1-method interface** in `src/.../Abstractions/`:
   ```csharp
   public interface IMyParser
   {
       MyResult Parse(string input);
   }
   ```

2. **Implement the default** (production) implementation in the same `src/` project:
   ```csharp
   internal sealed class DefaultMyParser : IMyParser
   {
       public MyResult Parse(string input) { /* real logic */ }
   }
   ```

3. **Accept as optional constructor param** in the orchestrator:
   ```csharp
   public MyEngine(IMyParser? parser = null)
   {
       _parser = parser ?? new DefaultMyParser();
   }
   ```

4. **Write a Fake** in `test/.../Fakes/`:
   ```csharp
   internal sealed class FakeMyParser : IMyParser
   {
       private readonly MyResult _result;
       public FakeMyParser(MyResult result) => _result = result;
       public MyResult Parse(string input) => _result;
   }
   ```

Reference: `QualityGate/src/.../Abstractions/ISolutionLoader.cs` + `QualityGate/test/.../Fakes/FakeSolutionLoader.cs`

## Adding a New Extension Point

When a source generator needs domain-specific customization:

1. **Add a nullable property with a default** to the model in `.SourceGenerator.Lib`:
   ```csharp
   public string? MyNewExtension { get; init; }  // null = no customization
   ```

2. **Use it conditionally** in the emitter:
   ```csharp
   if (model.MyNewExtension is not null)
       sb.AppendLine(model.MyNewExtension);
   ```

3. **Set it from the domain SG** that needs it. Other SGs pass null (the default) and are unaffected.

Reference: `Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/BuilderEmitModel.cs`

## Adding a New Static Analyzer

1. **Create a static class** in `QualityGate/src/.../Analysis/`:
   ```csharp
   internal static class MyMetricAnalyzer
   {
       public static MyMetricResult Analyze(Compilation compilation) { /* pure logic */ }
   }
   ```

2. **Call it from `ProjectAnalyzer`** alongside existing analyzers.

3. **Add results to the report model** in `QualityGate/src/.../Model/`.

4. **Add gate evaluation** in `QualityGate/src/.../Gates/QualityGateEvaluator.cs`.

5. **Write tests** using `RoslynTestHelper.CreateProject()` — no MSBuild needed.

Reference: `QualityGate/src/.../Analysis/ComplexityAnalyzer.cs`

## "Is This SOLID?" Checklist

Before submitting code that introduces a new abstraction or modifies an existing one:

- [ ] Does every new interface have exactly 1 method?
- [ ] Is every dependency injected via constructor (not resolved from a container)?
- [ ] Does every new interface have a corresponding Fake in `test/.../Fakes/`?
- [ ] Are new extension points backward-compatible (defaults for all new properties)?
- [ ] Are new analyzers/services stateless static classes (or justified otherwise)?
