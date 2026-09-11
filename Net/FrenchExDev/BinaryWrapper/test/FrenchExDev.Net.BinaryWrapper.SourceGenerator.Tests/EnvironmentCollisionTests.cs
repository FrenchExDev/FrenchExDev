using FrenchExDev.Net.BinaryWrapper.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator.Tests;

public sealed class EnvironmentCollisionTests
{
    [Theory]
    [InlineData(true, false, "EnvironmentOpt", null)]
    [InlineData(false, true, null, "EnvironmentArg")]
    [InlineData(true, true, "OptEnvironment", "ArgEnvironment")]
    public void Emit_EnvironmentCliMembers_UseSameDistinctNamesInCommandAndBuilder(
        bool hasOption, bool hasArgument, string? optionName, string? argumentName)
    {
        var descriptor = new DescriptorModel("TestNs", "ToolDescriptor", "tool", "--", " ", false, null);
        var command = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = hasOption
                ? [new() { LongName = "environment", ClrType = "string", ValueKind = "single" }]
                : [],
            Arguments = hasArgument
                ? [new() { Name = "environment", ClrType = "string", Position = 0 }]
                : []
        };

        var commandSource = CommandClassEmitter.Emit(descriptor, command);
        var builderSource = BuilderClassEmitter.Emit(descriptor, command);

        commandSource.ShouldContain("IReadOnlyDictionary<string, string> Environment { get; init; }");
        commandSource.ShouldNotContain("public string? Environment {");
        foreach (var memberName in new[] { optionName, argumentName }.OfType<string>())
        {
            commandSource.ShouldContain($"public string? {memberName} {{ get; init; }}");
            builderSource.ShouldContain($"With{memberName}(");
        }
        if (hasOption)
            commandSource.ShouldContain("__args.Add(\"--environment\")");
    }
}
