using FrenchExDev.Net.QualityGate.Config;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class QualityGateConfigTests
{
    [Fact]
    public void Default_HasReasonableDefaults()
    {
        var config = QualityGateConfig.Default();

        config.Solution.ShouldBeNull();
        config.Output.ShouldBe(".quality-gate/");
        config.Gates.ShouldNotBeNull();
        config.Gates.MaxCyclomaticComplexity.ShouldBe(15);
        config.Gates.MaxCognitiveComplexity.ShouldBe(20);
        config.Gates.MaxClassCoupling.ShouldBe(20);
        config.Gates.MaxInheritanceDepth.ShouldBe(5);
        config.Gates.MinMaintainabilityIndex.ShouldBe(60);
        config.Gates.MaxLcom.ShouldBe(3);
        config.Gates.MaxDuplicationPercent.ShouldBe(5);
        config.Gates.MinTestQualityScore.ShouldBe(0.80);
    }

    [Fact]
    public void Load_ValidYaml_ParsesAllFields()
    {
        var yaml = """
            solution: MySolution.slnx
            coverage:
              - "**/coverage.xml"
            mutations:
              - "**/mutation.json"
            output: my-output/
            gates:
              max-cyclomatic-complexity: 10
              max-cognitive-complexity: 15
              max-class-coupling: 25
              max-inheritance-depth: 4
              min-maintainability-index: 50
              max-lcom: 2
              max-distance-from-main-sequence: 0.4
              max-duplication-percent: 3
              min-test-quality-score: 0.90
            exclude:
              - "**/obj/**"
            """;

        var path = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}.yml");
        try
        {
            File.WriteAllText(path, yaml);
            var config = QualityGateConfig.Load(path);

            config.Solution.ShouldBe("MySolution.slnx");
            config.Output.ShouldBe("my-output/");
            config.CoverageGlobs.ShouldNotBeNull();
            config.CoverageGlobs.Count.ShouldBe(1);
            config.MutationGlobs.ShouldNotBeNull();
            config.MutationGlobs.Count.ShouldBe(1);
            config.ExcludePatterns.ShouldNotBeNull();
            config.ExcludePatterns.Count.ShouldBe(1);

            config.Gates.MaxCyclomaticComplexity.ShouldBe(10);
            config.Gates.MaxCognitiveComplexity.ShouldBe(15);
            config.Gates.MaxClassCoupling.ShouldBe(25);
            config.Gates.MaxInheritanceDepth.ShouldBe(4);
            config.Gates.MinMaintainabilityIndex.ShouldBe(50);
            config.Gates.MaxLcom.ShouldBe(2);
            config.Gates.MaxDistanceFromMainSequence.ShouldBe(0.4);
            config.Gates.MaxDuplicationPercent.ShouldBe(3);
            config.Gates.MinTestQualityScore.ShouldBe(0.90);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MinimalYaml_UsesDefaults()
    {
        var yaml = "solution: Test.slnx";

        var path = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}.yml");
        try
        {
            File.WriteAllText(path, yaml);
            var config = QualityGateConfig.Load(path);

            config.Solution.ShouldBe("Test.slnx");
            config.Output.ShouldBe(".quality-gate/");
            config.Gates.MaxCyclomaticComplexity.ShouldBe(15);
            config.CoverageGlobs.ShouldBeNull();
            config.MutationGlobs.ShouldBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_EmptyYaml_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}.yml");
        try
        {
            File.WriteAllText(path, "");
            var config = QualityGateConfig.Load(path);

            config.ShouldNotBeNull();
            config.Gates.ShouldNotBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
